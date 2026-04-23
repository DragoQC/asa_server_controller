using System.Security.Cryptography;
using asa_server_controller.Constants;
using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Invitations;
using asa_server_controller.Models.Servers;
using asa_server_controller.Models.Vpn;
using Microsoft.EntityFrameworkCore;
using asa_server_controller.Models.Cluster;

namespace asa_server_controller.Services;

public sealed class InvitationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    VpnService vpnService,
    ClusterSettingsService clusterSettingsService,
    InvitationEventsService invitationEventsService,
    RemoteServerHubClientService remoteServerHubClientService)
{
    private const int DefaultRemoteServerPort = 8000;

    public async Task<IReadOnlyList<InvitationListItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<InvitationListItem> items = await dbContext.Invitations
            .Select(invitation => new InvitationListItem(
                invitation.Id,
                invitation.RemoteServerId,
                invitation.RemoteUrl,
                invitation.ClusterId,
                invitation.RemoteServer.VpnAddress,
                invitation.InviteLink,
                invitation.InviteStatus,
                invitation.RemoteServer.ValidationStatus,
                invitation.UsedAtUtc,
                invitation.RemoteServer.LastSeenAtUtc,
                invitation.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .ToList();
    }

    public async Task<VpnInviteFormModel> LoadVpnFormAsync(CancellationToken cancellationToken = default)
    {
        ClusterSettingsModel clusterSettings = await clusterSettingsService.LoadAsync(cancellationToken);
        bool isVpnInstalled = await vpnService.IsVpnInstalledAsync(cancellationToken);

        if (!isVpnInstalled)
        {
            return new VpnInviteFormModel
            {
                IsReady = false,
                IsVpnInstalled = false,
                StatusMessage = "Install WireGuard on the Cluster setup page before creating VPN invitations.",
                ClusterId = clusterSettings.ClusterId ?? string.Empty,
                Port = DefaultRemoteServerPort.ToString()
            };
        }

        if (!await vpnService.IsConfiguredAsync(cancellationToken))
        {
            return new VpnInviteFormModel
            {
                IsReady = false,
                IsVpnInstalled = true,
                StatusMessage = "Finish the VPN setup on the Cluster setup page before creating VPN invitations.",
                ClusterId = clusterSettings.ClusterId ?? string.Empty,
                Port = DefaultRemoteServerPort.ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(clusterSettings.ClusterId))
        {
            return new VpnInviteFormModel
            {
                IsReady = false,
                IsVpnInstalled = true,
                StatusMessage = "Set the cluster ID on the Cluster setup page before creating VPN invitations.",
                ClusterId = string.Empty,
                Port = DefaultRemoteServerPort.ToString()
            };
        }

        VpnConfigModel currentConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        int inviteCount = await dbContext.Invitations.CountAsync(cancellationToken);

        return new VpnInviteFormModel
        {
            IsReady = true,
            IsVpnInstalled = true,
            ClusterId = clusterSettings.ClusterId,
            VpnAddress = GetNextVpnAddress(currentConfig.Address, inviteCount),
            Port = DefaultRemoteServerPort.ToString()
        };
    }

    public async Task<InviteRemoteServerRequest> BuildVpnPreviewAsync(InvitationFormModel form, CancellationToken cancellationToken = default)
    {
        VpnInviteFormModel vpnForm = await LoadVpnFormAsync(cancellationToken);
        if (!vpnForm.IsReady)
        {
            throw new InvalidOperationException(vpnForm.StatusMessage ?? "VPN invitations are not ready.");
        }

        if (string.IsNullOrWhiteSpace(form.VpnAddress))
        {
            throw new InvalidOperationException("VPN address is required.");
        }

        string clusterId = await clusterSettingsService.LoadRequiredClusterIdAsync(cancellationToken);
        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnService.LoadServerKeyPairAsync(cancellationToken);
        RemoteServerEntity remoteServer = new()
        {
            VpnAddress = form.VpnAddress.Trim(),
            Port = ParsePortOrDefault(form.Port),
            InviteStatus = "Preview",
            ValidationStatus = "Preview",
            ApiKey = "(generated on link creation)"
        };

        InvitationEntity previewInvitation = new()
        {
            RemoteServerId = 0,
            RemoteServer = remoteServer,
            RemoteUrl = string.Empty,
            ClusterId = clusterId,
            OneTimeVpnKey = "preview",
            InviteLink = string.Empty,
            InviteStatus = "Preview",
            ValidationStatus = "Preview"
        };

        VpnKeyPair previewClientKeys = await vpnService.GenerateKeyPairAsync(cancellationToken);
        string invitationConfigPreview = BuildInvitationConfigPreview(vpnConfig, remoteServer.VpnAddress, previewClientKeys.PrivateKey, serverKeys);
        return BuildInviteRequest(previewInvitation, vpnConfig, previewClientKeys.PrivateKey, invitationConfigPreview, serverKeys);
    }

    public async Task<InvitationListItem> CreateVpnInvitationLinkAsync(InvitationFormModel form, CancellationToken cancellationToken = default)
    {
        VpnInviteFormModel vpnForm = await LoadVpnFormAsync(cancellationToken);
        if (!vpnForm.IsReady)
        {
            throw new InvalidOperationException(vpnForm.StatusMessage ?? "VPN invitations are not ready.");
        }

        if (string.IsNullOrWhiteSpace(form.VpnAddress))
        {
            throw new InvalidOperationException("VPN address is required.");
        }

        string clusterId = await clusterSettingsService.LoadRequiredClusterIdAsync(cancellationToken);
        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnService.LoadServerKeyPairAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(vpnConfig.Endpoint))
        {
            throw new InvalidOperationException("VPN endpoint is required before creating invitation links.");
        }

        if (string.IsNullOrWhiteSpace(vpnConfig.ListenPort))
        {
            throw new InvalidOperationException("VPN listen port is required before creating invitation links.");
        }

        if (string.IsNullOrWhiteSpace(vpnConfig.AllowedIps))
        {
            throw new InvalidOperationException("VPN allowed IPs are required before creating invitation links.");
        }

        if (string.IsNullOrWhiteSpace(serverKeys.PublicKey))
        {
            throw new InvalidOperationException("Generate server keys before creating invitation links.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string vpnAddress = form.VpnAddress.Trim();
        int parsedPort = ParsePortOrDefault(form.Port);
        await RemoveAbandonedPendingRemoteServerReservationAsync(dbContext, vpnAddress, cancellationToken);

        bool vpnAddressAlreadyExists = await dbContext.RemoteServers
            .AnyAsync(server => server.VpnAddress == vpnAddress, cancellationToken);

        if (vpnAddressAlreadyExists)
        {
            throw new InvalidOperationException("VPN address is already assigned to another remote server.");
        }

        string oneTimeVpnKey = GenerateOneTimeVpnKey();
        string inviteLink = BuildInviteLink(vpnConfig.Endpoint, vpnConfig.ListenPort, oneTimeVpnKey);
        string remoteApiKey = GenerateRemoteApiKey();
        string remoteUrl = new RemoteServerConnection(0, vpnAddress, parsedPort, remoteApiKey).BaseUrl;
        VpnKeyPair invitationClientKeys = await vpnService.GenerateKeyPairAsync(cancellationToken);

        await vpnService.SaveInvitationFilesAsync(
            oneTimeVpnKey,
            vpnConfig,
            vpnAddress,
            invitationClientKeys.PrivateKey,
            invitationClientKeys.PublicKey,
            serverKeys.PublicKey!,
            cancellationToken);

        RemoteServerEntity remoteServer = new()
        {
            VpnAddress = vpnAddress,
            Port = parsedPort,
            InviteStatus = "Pending",
            ValidationStatus = "Not claimed",
            ApiKey = remoteApiKey
        };

        InvitationEntity invitation = new()
        {
            RemoteServer = remoteServer,
            RemoteUrl = remoteUrl,
            ClusterId = clusterId,
            OneTimeVpnKey = oneTimeVpnKey,
            InviteLink = inviteLink,
            InviteStatus = "Pending",
            ValidationStatus = "Not claimed"
        };

        try
        {
            dbContext.RemoteServers.Add(remoteServer);
            dbContext.Invitations.Add(invitation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await vpnService.DeleteInvitationFilesAsync(oneTimeVpnKey, cancellationToken: cancellationToken);
            throw;
        }

        await RebuildServerConfigAsync(cancellationToken);
        await RestartWireGuardIfActiveAsync(cancellationToken);

        return new InvitationListItem(
            invitation.Id,
            invitation.RemoteServerId,
            invitation.RemoteUrl,
            invitation.ClusterId,
            remoteServer.VpnAddress,
            invitation.InviteLink,
            invitation.InviteStatus,
            remoteServer.ValidationStatus,
            invitation.UsedAtUtc,
            remoteServer.LastSeenAtUtc,
            invitation.CreatedAtUtc);
    }

    public async Task<InviteRemoteServerRequest> GetInviteRequestAsync(string inviteKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inviteKey))
        {
            throw new InvalidOperationException("VPN invite key is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        InvitationEntity? invitation = await dbContext.Invitations
            .Include(item => item.RemoteServer)
            .FirstOrDefaultAsync(item => item.OneTimeVpnKey == inviteKey.Trim(), cancellationToken);

        if (invitation is null)
        {
            throw new InvalidOperationException("VPN invite key is invalid.");
        }

        if (invitation.UsedAtUtc is not null)
        {
            throw new InvalidOperationException("VPN invite key has already been used.");
        }

        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnService.LoadServerKeyPairAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(serverKeys.PublicKey))
        {
            throw new InvalidOperationException("Server public key is not ready for invite claims.");
        }

        string invitationConfigContent = await vpnService.LoadInvitationConfigContentAsync(invitation.OneTimeVpnKey, invitation.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(invitationConfigContent))
        {
            throw new InvalidOperationException("Invitation wg0.conf is missing.");
        }

        string clientPrivateKey = await LoadRequiredInvitationClientPrivateKeyAsync(invitation, cancellationToken);

        invitation.UsedAtUtc = DateTimeOffset.UtcNow;
        invitation.InviteStatus = "Accepted";
        invitation.ValidationStatus = "Unknown";
        invitation.RemoteServer.InviteStatus = "Accepted";
        invitation.RemoteServer.ValidationStatus = "Unknown";
        await dbContext.SaveChangesAsync(cancellationToken);
        invitationEventsService.NotifyChanged();
        await remoteServerHubClientService.SynchronizeNowAsync(cancellationToken);

        return BuildInviteRequest(invitation, vpnConfig, clientPrivateKey, invitationConfigContent, serverKeys);
    }

    public async Task DeleteAsync(int invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId <= 0)
        {
            throw new InvalidOperationException("Invitation is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        InvitationEntity invitation = await dbContext.Invitations
            .Include(item => item.RemoteServer)
            .FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");

        bool shouldRemovePendingRemoteServer = invitation.UsedAtUtc is null &&
            !string.Equals(invitation.RemoteServer.InviteStatus, "Accepted", StringComparison.Ordinal);

        dbContext.Invitations.Remove(invitation);
        if (shouldRemovePendingRemoteServer)
        {
            dbContext.RemoteServers.Remove(invitation.RemoteServer);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await vpnService.DeleteInvitationFilesAsync(invitation.OneTimeVpnKey, invitation.Id, cancellationToken);
        await RebuildServerConfigAsync(cancellationToken);
        await RestartWireGuardIfActiveAsync(cancellationToken);
        invitationEventsService.NotifyChanged();
    }

    private static async Task RemoveAbandonedPendingRemoteServerReservationAsync(
        AppDbContext dbContext,
        string vpnAddress,
        CancellationToken cancellationToken)
    {
        List<RemoteServerEntity> abandonedServers = await dbContext.RemoteServers
            .Include(server => server.Invitations)
            .Where(server =>
                server.VpnAddress == vpnAddress &&
                server.InviteStatus != "Accepted" &&
                !server.Invitations.Any())
            .ToListAsync(cancellationToken);

        if (abandonedServers.Count == 0)
        {
            return;
        }

        dbContext.RemoteServers.RemoveRange(abandonedServers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> LoadInvitationConfigAsync(int invitationId, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await dbContext.Invitations.AnyAsync(item => item.Id == invitationId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Invitation does not exist.");
        }

        InvitationEntity invitation = await dbContext.Invitations
            .FirstAsync(item => item.Id == invitationId, cancellationToken);

        string content = await vpnService.LoadInvitationConfigContentAsync(invitation.OneTimeVpnKey, invitationId, cancellationToken);
        return string.IsNullOrWhiteSpace(content)
            ? throw new InvalidOperationException("Invitation wg0.conf is missing.")
            : content;
    }

    public async Task RebuildServerConfigAsync(CancellationToken cancellationToken = default)
    {
        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnService.LoadServerKeyPairAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(serverKeys.PrivateKey))
        {
            throw new InvalidOperationException("Server private key is required before rebuilding wg0.conf.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<InvitationEntity> invitations = await dbContext.Invitations
            .Include(item => item.RemoteServer)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        List<(string PublicKey, string AllowedIp, string? PresharedKey)> peers = [];

        foreach (InvitationEntity invitation in invitations)
        {
            string? clientPublicKey = await vpnService.LoadInvitationClientPublicKeyAsync(invitation.OneTimeVpnKey, invitation.Id, cancellationToken);
            if (string.IsNullOrWhiteSpace(clientPublicKey))
            {
                continue;
            }

            string allowedIp = invitation.RemoteServer.VpnAddress;
            peers.Add((clientPublicKey, allowedIp, string.IsNullOrWhiteSpace(vpnConfig.PresharedKey) ? null : vpnConfig.PresharedKey.Trim()));
        }

        string content = await vpnService.BuildServerConfigWithPeersAsync(vpnConfig, serverKeys.PrivateKey, peers, cancellationToken);
        await vpnService.SaveAsync(VpnConstants.VpnConfigFilePath, content, cancellationToken);
    }

    private static string GetNextVpnAddress(string? controlAddress, int inviteCount)
    {
        if (string.IsNullOrWhiteSpace(controlAddress))
        {
            throw new InvalidOperationException("VPN address must be configured in wg0.conf before generating invitations.");
        }

        string address = controlAddress.Trim();
        string[] addressParts = address.Split('/', 2, StringSplitOptions.TrimEntries);
        string ipPart = addressParts[0];
        string cidrPart = addressParts.Length > 1 ? addressParts[1] : "32";
        string[] octets = ipPart.Split('.', StringSplitOptions.TrimEntries);

        if (octets.Length == 4 &&
            int.TryParse(octets[0], out int firstOctet) &&
            int.TryParse(octets[1], out int secondOctet) &&
            int.TryParse(octets[2], out int thirdOctet) &&
            int.TryParse(octets[3], out int lastOctet))
        {
            int nextOctet = lastOctet + inviteCount + 1;
            return $"{firstOctet}.{secondOctet}.{thirdOctet}.{nextOctet}/{cidrPart}";
        }

        if (octets.Length >= 3 &&
            int.TryParse(octets[0], out int fallbackFirstOctet) &&
            int.TryParse(octets[1], out int fallbackSecondOctet) &&
            int.TryParse(octets[2], out int fallbackThirdOctet))
        {
            int nextOctet = 3 + inviteCount;
            return $"{fallbackFirstOctet}.{fallbackSecondOctet}.{fallbackThirdOctet}.{nextOctet}/{cidrPart}";
        }

        throw new InvalidOperationException("VPN address format in wg0.conf is invalid for invitation generation.");
    }

    private static string GenerateOneTimeVpnKey()
    {
        return Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
    }

    private static string GenerateRemoteApiKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    private static int ParsePortOrDefault(string? port)
    {
        if (string.IsNullOrWhiteSpace(port))
        {
            return DefaultRemoteServerPort;
        }

        string normalizedPort = port.Trim();
        if (!int.TryParse(normalizedPort, out int parsedPort) || parsedPort <= 0)
        {
            throw new InvalidOperationException("Port is invalid.");
        }

        return parsedPort;
    }

    private static string BuildInviteLink(string? endpoint, string? listenPort, string oneTimeVpnKey)
    {
        string host = string.IsNullOrWhiteSpace(endpoint)
            ? throw new InvalidOperationException("VPN endpoint is required before generating an invite link.")
            : NormalizeInviteEndpoint(endpoint.Trim(), listenPort);

        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"{host.TrimEnd('/')}/api/vpn/invite/{oneTimeVpnKey}";
        }

        return $"https://{host}/api/vpn/invite/{oneTimeVpnKey}";
    }

    private static string NormalizeInviteEndpoint(string endpoint, string? listenPort)
    {
        if (string.IsNullOrWhiteSpace(listenPort))
        {
            return endpoint;
        }

        string suffix = $":{listenPort.Trim()}";
        if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            endpoint.EndsWith(suffix, StringComparison.Ordinal))
        {
            return endpoint[..^suffix.Length];
        }

        return endpoint;
    }

    private static InviteRemoteServerRequest BuildInviteRequest(
        InvitationEntity invitation,
        VpnConfigModel vpnConfig,
        string clientPrivateKey,
        string wg0Config,
        SavedVpnKeyPair serverKeys)
    {
        string endpoint = string.IsNullOrWhiteSpace(vpnConfig.Endpoint)
            ? throw new InvalidOperationException("VPN endpoint is required before creating an invite request.")
            : vpnConfig.Endpoint.Trim();
        string listenPort = string.IsNullOrWhiteSpace(vpnConfig.ListenPort)
            ? throw new InvalidOperationException("VPN listen port is required before creating an invite request.")
            : vpnConfig.ListenPort.Trim();
        string allowedIps = string.IsNullOrWhiteSpace(vpnConfig.AllowedIps)
            ? throw new InvalidOperationException("VPN allowed IPs are required before creating an invite request.")
            : vpnConfig.AllowedIps.Trim();
        string serverPublicKey = string.IsNullOrWhiteSpace(serverKeys.PublicKey)
            ? throw new InvalidOperationException("Server public key is required before creating an invite request.")
            : serverKeys.PublicKey.Trim();
        string trimmedClientPrivateKey = string.IsNullOrWhiteSpace(clientPrivateKey)
            ? throw new InvalidOperationException("Client private key is required before creating an invite request.")
            : clientPrivateKey.Trim();
        string trimmedWg0Config = string.IsNullOrWhiteSpace(wg0Config)
            ? throw new InvalidOperationException("Invitation wg0.conf is required before creating an invite request.")
            : wg0Config;

        string remoteApiKey = string.IsNullOrWhiteSpace(invitation.RemoteServer.ApiKey)
            ? throw new InvalidOperationException("Remote server API key is required before creating an invite request.")
            : invitation.RemoteServer.ApiKey.Trim();

        return new InviteRemoteServerRequest(
            invitation.ClusterId,
            invitation.RemoteServer.VpnAddress,
            $"{endpoint}:{listenPort}",
            allowedIps,
            remoteApiKey,
            serverPublicKey,
            trimmedClientPrivateKey,
            trimmedWg0Config,
            string.IsNullOrWhiteSpace(vpnConfig.PresharedKey) ? null : vpnConfig.PresharedKey.Trim());
    }

    private async Task<string> LoadRequiredInvitationClientPrivateKeyAsync(InvitationEntity invitation, CancellationToken cancellationToken)
    {
        string filePath = vpnService.GetInvitationClientPrivateKeyFilePath(invitation.OneTimeVpnKey);
        if (!File.Exists(filePath))
        {
            filePath = vpnService.GetInvitationClientPrivateKeyFilePath(invitation.Id);
        }

        string content = await vpnService.LoadEditorContentAsync(filePath, cancellationToken);

        return string.IsNullOrWhiteSpace(content)
            ? throw new InvalidOperationException("Invitation client private key is missing.")
            : content.Trim();
    }

    private static string BuildInvitationConfigPreview(
        VpnConfigModel vpnConfig,
        string vpnAddress,
        string clientPrivateKey,
        SavedVpnKeyPair serverKeys)
    {
        if (string.IsNullOrWhiteSpace(serverKeys.PublicKey))
        {
            throw new InvalidOperationException("Server public key is required before creating an invite preview.");
        }

        List<string> lines =
        [
            "[Interface]",
            $"PrivateKey = {clientPrivateKey.Trim()}",
            $"Address = {vpnAddress.Trim()}"
        ];

        lines.Add(string.Empty);
        lines.Add("[Peer]");
        lines.Add($"PublicKey = {serverKeys.PublicKey.Trim()}");

        if (!string.IsNullOrWhiteSpace(vpnConfig.PresharedKey))
        {
            lines.Add($"PresharedKey = {vpnConfig.PresharedKey.Trim()}");
        }

        lines.Add($"Endpoint = {BuildEndpointWithPort(vpnConfig)}");
        lines.Add($"AllowedIPs = {vpnConfig.AllowedIps?.Trim()}");

        if (!string.IsNullOrWhiteSpace(vpnConfig.PersistentKeepalive))
        {
            lines.Add($"PersistentKeepalive = {vpnConfig.PersistentKeepalive.Trim()}");
        }

        return string.Join('\n', lines).TrimEnd() + "\n";
    }

    private static string BuildEndpointWithPort(VpnConfigModel vpnConfig)
    {
        string endpoint = string.IsNullOrWhiteSpace(vpnConfig.Endpoint)
            ? throw new InvalidOperationException("VPN endpoint is required before creating an invite request.")
            : vpnConfig.Endpoint.Trim();
        string listenPort = string.IsNullOrWhiteSpace(vpnConfig.ListenPort)
            ? throw new InvalidOperationException("VPN listen port is required before creating an invite request.")
            : vpnConfig.ListenPort.Trim();

        return endpoint.Contains(':', StringComparison.Ordinal) ? endpoint : $"{endpoint}:{listenPort}";
    }

    private async Task RestartWireGuardIfActiveAsync(CancellationToken cancellationToken)
    {
        if (!await vpnService.IsVpnActiveAsync(cancellationToken))
        {
            return;
        }

        await vpnService.RestartVpnAsync(cancellationToken);
    }

}
