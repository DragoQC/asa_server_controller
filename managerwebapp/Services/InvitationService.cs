using System.Security.Cryptography;
using managerwebapp.Constants;
using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Invitations;
using managerwebapp.Models.Vpn;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Services;

public sealed class InvitationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    VpnConfigService vpnConfigService,
    ClusterSettingsService clusterSettingsService,
    SudoService sudoService)
{
    private const int DefaultRemoteServerPort = 8000;

    public async Task<IReadOnlyList<InvitationListItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<InvitationListItem> items = await dbContext.Invitations
            .Select(invitation => new InvitationListItem(
                invitation.Id,
                invitation.RemoteUrl,
                invitation.ClusterId,
                invitation.VpnAddress,
                invitation.InviteLink,
                invitation.InviteStatus,
                invitation.ValidationStatus,
                invitation.UsedAtUtc,
                invitation.LastSeenAtUtc,
                invitation.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .ToList();
    }

    public async Task<InvitationFormModel> CreateDefaultFormAsync(CancellationToken cancellationToken = default)
    {
        VpnConfigModel currentConfig = await vpnConfigService.LoadConfiguredModelAsync(cancellationToken);
        Models.Cluster.ClusterSettingsModel clusterSettings = await clusterSettingsService.LoadAsync(cancellationToken);
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        int inviteCount = await dbContext.Invitations.CountAsync(cancellationToken);

        return new InvitationFormModel
        {
            ClusterId = clusterSettings.ClusterId,
            VpnAddress = GetNextVpnAddress(currentConfig.Address, inviteCount)
        };
    }

    public async Task<InviteRemoteServerRequest> BuildPreviewAsync(InvitationFormModel form, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(form.VpnAddress))
        {
            throw new InvalidOperationException("VPN address is required.");
        }

        string clusterId = await clusterSettingsService.LoadRequiredClusterIdAsync(cancellationToken);
        VpnConfigModel vpnConfig = await vpnConfigService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnConfigService.LoadServerKeyPairAsync(cancellationToken);

        InvitationEntity previewInvitation = new()
        {
            RemoteUrl = string.Empty,
            ClusterId = clusterId,
            VpnAddress = form.VpnAddress.Trim(),
            RemoteApiKey = "(generated on link creation)",
            OneTimeVpnKey = "preview",
            InviteLink = string.Empty,
            InviteStatus = "Preview",
            ValidationStatus = "Preview"
        };

        VpnKeyPair previewClientKeys = await vpnConfigService.GenerateKeyPairAsync(cancellationToken);
        string invitationConfigPreview = BuildInvitationConfigPreview(vpnConfig, previewInvitation.VpnAddress, previewClientKeys.PrivateKey, serverKeys);
        return BuildInviteRequest(previewInvitation, vpnConfig, previewClientKeys.PrivateKey, invitationConfigPreview, serverKeys);
    }

    public async Task<InvitationListItem> CreateInvitationLinkAsync(InvitationFormModel form, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(form.VpnAddress))
        {
            throw new InvalidOperationException("VPN address is required.");
        }

        string clusterId = await clusterSettingsService.LoadRequiredClusterIdAsync(cancellationToken);
        VpnConfigModel vpnConfig = await vpnConfigService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnConfigService.LoadServerKeyPairAsync(cancellationToken);

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

        VpnKeyPair invitationClientKeys = await vpnConfigService.GenerateKeyPairAsync(cancellationToken);

        InvitationEntity invitation = new()
        {
            RemoteUrl = string.Empty,
            ClusterId = clusterId,
            VpnAddress = form.VpnAddress.Trim(),
            RemoteApiKey = GenerateRemoteApiKey(),
            OneTimeVpnKey = GenerateOneTimeVpnKey(),
            InviteLink = string.Empty,
            InviteStatus = "Pending",
            ValidationStatus = "Not claimed"
        };

        invitation.InviteLink = BuildInviteLink(vpnConfig.Endpoint, vpnConfig.ListenPort, invitation.OneTimeVpnKey);

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await vpnConfigService.SaveInvitationFilesAsync(
            invitation.Id,
            vpnConfig,
            invitation.VpnAddress,
            invitationClientKeys.PrivateKey,
            invitationClientKeys.PublicKey,
            serverKeys.PublicKey!,
            cancellationToken);

        await RebuildServerConfigAsync(cancellationToken);
        await RestartWireGuardIfActiveAsync(cancellationToken);

        return new InvitationListItem(
            invitation.Id,
            invitation.RemoteUrl,
            invitation.ClusterId,
            invitation.VpnAddress,
            invitation.InviteLink,
            invitation.InviteStatus,
            invitation.ValidationStatus,
            invitation.UsedAtUtc,
            invitation.LastSeenAtUtc,
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
            .FirstOrDefaultAsync(item => item.OneTimeVpnKey == inviteKey.Trim(), cancellationToken);

        if (invitation is null)
        {
            throw new InvalidOperationException("VPN invite key is invalid.");
        }

        if (invitation.UsedAtUtc is not null)
        {
            throw new InvalidOperationException("VPN invite key has already been used.");
        }

        VpnConfigModel vpnConfig = await vpnConfigService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnConfigService.LoadServerKeyPairAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(serverKeys.PublicKey))
        {
            throw new InvalidOperationException("Server public key is not ready for invite claims.");
        }

        string invitationConfigContent = await vpnConfigService.LoadInvitationConfigContentAsync(invitation.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(invitationConfigContent))
        {
            throw new InvalidOperationException("Invitation wg0.conf is missing.");
        }

        string clientPrivateKey = await LoadRequiredInvitationClientPrivateKeyAsync(invitation.Id, cancellationToken);

        invitation.UsedAtUtc = DateTimeOffset.UtcNow;
        invitation.InviteStatus = "Accepted";
        invitation.ValidationStatus = "Unknown";
        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildInviteRequest(invitation, vpnConfig, clientPrivateKey, invitationConfigContent, serverKeys);
    }

    public async Task<string> LoadInvitationConfigAsync(int invitationId, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await dbContext.Invitations.AnyAsync(item => item.Id == invitationId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Invitation does not exist.");
        }

        string content = await vpnConfigService.LoadInvitationConfigContentAsync(invitationId, cancellationToken);
        return string.IsNullOrWhiteSpace(content)
            ? throw new InvalidOperationException("Invitation wg0.conf is missing.")
            : content;
    }

    public async Task RebuildServerConfigAsync(CancellationToken cancellationToken = default)
    {
        VpnConfigModel vpnConfig = await vpnConfigService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnConfigService.LoadServerKeyPairAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(serverKeys.PrivateKey))
        {
            throw new InvalidOperationException("Server private key is required before rebuilding wg0.conf.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<InvitationEntity> invitations = await dbContext.Invitations
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        List<(string PublicKey, string AllowedIp, string? PresharedKey)> peers = [];

        foreach (InvitationEntity invitation in invitations)
        {
            string? clientPublicKey = await vpnConfigService.LoadInvitationClientPublicKeyAsync(invitation.Id, cancellationToken);
            if (string.IsNullOrWhiteSpace(clientPublicKey))
            {
                continue;
            }

            peers.Add((clientPublicKey, invitation.VpnAddress, string.IsNullOrWhiteSpace(vpnConfig.PresharedKey) ? null : vpnConfig.PresharedKey.Trim()));
        }

        string content = await vpnConfigService.BuildServerConfigWithPeersAsync(vpnConfig, serverKeys.PrivateKey, peers, cancellationToken);
        await vpnConfigService.SaveAsync(VpnConstants.VpnConfigFilePath, content, cancellationToken);
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

        return new InviteRemoteServerRequest(
            invitation.ClusterId,
            invitation.VpnAddress,
            $"{endpoint}:{listenPort}",
            allowedIps,
            invitation.RemoteApiKey,
            serverPublicKey,
            trimmedClientPrivateKey,
            trimmedWg0Config,
            string.IsNullOrWhiteSpace(vpnConfig.PresharedKey) ? null : vpnConfig.PresharedKey.Trim());
    }

    private async Task<string> LoadRequiredInvitationClientPrivateKeyAsync(int invitationId, CancellationToken cancellationToken)
    {
        string filePath = vpnConfigService.GetInvitationClientPrivateKeyFilePath(invitationId);
        string content = await vpnConfigService.LoadEditorContentAsync(filePath, cancellationToken);

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
        if (!await sudoService.IsWireGuardActiveAsync(cancellationToken))
        {
            return;
        }

        await sudoService.RestartWireGuardAsync(cancellationToken);
    }

}
