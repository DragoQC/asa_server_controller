using System.Security.Cryptography;
using asa_server_controller.Constants;
using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Cluster;
using asa_server_controller.Models.Vpn;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class NfsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    VpnService vpnService,
    SudoService sudoService,
    InvitationEventsService invitationEventsService)
{
    public async Task<NfsConfigurationModel> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        string currentAddress = await vpnService.LoadCurrentAddressAsync(cancellationToken);
        string currentIpAddress = await vpnService.LoadCurrentIpAddressAsync(cancellationToken);

        bool clusterFolderExists = Directory.Exists(ClusterShareConstants.ClusterDirectoryPath);
        bool serverConfigExists = File.Exists(ClusterShareConstants.ServerConfigFilePath);
        bool clientConfigExists = File.Exists(ClusterShareConstants.ClientConfigFilePath);

        string serverConfigContent = serverConfigExists
            ? File.ReadAllText(ClusterShareConstants.ServerConfigFilePath)
            : BuildServerConfig(currentAddress);

        string clientConfigContent = clientConfigExists
            ? File.ReadAllText(ClusterShareConstants.ClientConfigFilePath)
            : BuildClientConfig(currentIpAddress);

        return new NfsConfigurationModel(
            clusterFolderExists,
            serverConfigExists,
            clientConfigExists,
            ClusterShareConstants.ClusterDirectoryPath,
            ClusterShareConstants.ServerConfigFilePath,
            ClusterShareConstants.ClientConfigFilePath,
            Normalize(serverConfigContent),
            Normalize(clientConfigContent));
    }

    public async Task<NfsConfigurationModel> CreateDefaultConfigAsync(CancellationToken cancellationToken = default)
    {
        string configuredAddress = await vpnService.LoadConfiguredAddressAsync(cancellationToken);
        string configuredIpAddress = await vpnService.LoadConfiguredIpAddressAsync(cancellationToken);

        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.NfsDirectoryPath);

        string serverConfig = BuildServerConfig(configuredAddress);
        string clientConfig = BuildClientConfig(configuredIpAddress);

        await File.WriteAllTextAsync(ClusterShareConstants.ServerConfigFilePath, serverConfig, cancellationToken);
        await File.WriteAllTextAsync(ClusterShareConstants.ClientConfigFilePath, clientConfig, cancellationToken);
        await sudoService.ApplyNfsServerAsync(cancellationToken);

        return await LoadConfigurationAsync(cancellationToken);
    }

    public async Task<NfsConfigurationModel> SaveConfigurationAsync(string serverConfigContent, string clientConfigContent, CancellationToken cancellationToken = default)
    {
        await vpnService.LoadConfiguredAddressAsync(cancellationToken);

        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.NfsDirectoryPath);

        await File.WriteAllTextAsync(ClusterShareConstants.ServerConfigFilePath, Normalize(serverConfigContent), cancellationToken);
        await File.WriteAllTextAsync(ClusterShareConstants.ClientConfigFilePath, Normalize(clientConfigContent), cancellationToken);
        await sudoService.ApplyNfsServerAsync(cancellationToken);

        return await LoadConfigurationAsync(cancellationToken);
    }

    public async Task SyncDefaultConfigIfExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ClusterShareConstants.ServerConfigFilePath) &&
            !File.Exists(ClusterShareConstants.ClientConfigFilePath))
        {
            return;
        }

        string configuredAddress = await vpnService.LoadConfiguredAddressAsync(cancellationToken);
        string configuredIpAddress = await vpnService.LoadConfiguredIpAddressAsync(cancellationToken);

        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.NfsDirectoryPath);

        if (File.Exists(ClusterShareConstants.ServerConfigFilePath))
        {
            await File.WriteAllTextAsync(
                ClusterShareConstants.ServerConfigFilePath,
                BuildServerConfig(configuredAddress),
                cancellationToken);
            await sudoService.ApplyNfsServerAsync(cancellationToken);
        }

        if (File.Exists(ClusterShareConstants.ClientConfigFilePath))
        {
            await File.WriteAllTextAsync(
                ClusterShareConstants.ClientConfigFilePath,
                BuildClientConfig(configuredIpAddress),
                cancellationToken);
        }
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        if (!await vpnService.IsConfiguredAsync(cancellationToken))
        {
            return false;
        }

        return Directory.Exists(ClusterShareConstants.ClusterDirectoryPath) &&
            File.Exists(ClusterShareConstants.ServerConfigFilePath) &&
            File.Exists(ClusterShareConstants.ClientConfigFilePath);
    }

    public async Task<NfsInviteFormModel> LoadNfsFormAsync(CancellationToken cancellationToken = default)
    {
        if (!await vpnService.IsConfiguredAsync(cancellationToken))
        {
            return new NfsInviteFormModel
            {
                IsReady = false,
                StatusMessage = "Finish the VPN setup on the Cluster setup page before creating NFS invitations."
            };
        }

        if (!await IsConfiguredAsync(cancellationToken))
        {
            return new NfsInviteFormModel
            {
                IsReady = false,
                StatusMessage = "Save the NFS configuration on the Cluster setup page before creating NFS invitations."
            };
        }

        return new NfsInviteFormModel
        {
            IsReady = true
        };
    }

    public async Task<IReadOnlyList<NfsShareInviteServerOption>> LoadTargetServersAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.RemoteServers
            .Where(server => server.InviteStatus == "Accepted" && !server.NfsShareInvites.Any())
            .OrderBy(server => server.VpnAddress)
            .Select(server => new NfsShareInviteServerOption(
                server.Id,
                server.VpnAddress,
                server.Port))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NfsShareInviteListItem>> LoadInvitesAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<NfsShareInviteListItem> items = await dbContext.NfsShareInvites
            .Select(invite => new NfsShareInviteListItem(
                invite.Id,
                invite.RemoteServerId,
                invite.RemoteServer.VpnAddress,
                invite.InviteLink,
                invite.UsedAtUtc,
                invite.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .ToList();
    }

    public async Task<NfsShareInviteResponse> BuildNfsPreviewAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        NfsInviteFormModel form = await LoadNfsFormAsync(cancellationToken);
        if (!form.IsReady)
        {
            throw new InvalidOperationException(form.StatusMessage ?? "NFS invitations are not ready.");
        }

        if (remoteServerId <= 0)
        {
            throw new InvalidOperationException("Remote server is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await dbContext.RemoteServers
            .AnyAsync(server => server.Id == remoteServerId && server.InviteStatus == "Accepted", cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Accepted remote server is required for an NFS invite.");
        }

        NfsConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);

        return new NfsShareInviteResponse(
            ClusterShareConstants.ClusterDirectoryPath,
            ClusterShareConstants.ClientMountPath,
            configuration.ClientConfigContent);
    }

    public async Task<string> CreateNfsInvitationLinkAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        NfsInviteFormModel form = await LoadNfsFormAsync(cancellationToken);
        if (!form.IsReady)
        {
            throw new InvalidOperationException(form.StatusMessage ?? "NFS invitations are not ready.");
        }

        if (remoteServerId <= 0)
        {
            throw new InvalidOperationException("Remote server is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        RemoteServerEntity? remoteServer = await dbContext.RemoteServers
            .FirstOrDefaultAsync(server => server.Id == remoteServerId && server.InviteStatus == "Accepted", cancellationToken);

        if (remoteServer is null)
        {
            throw new InvalidOperationException("Accepted remote server was not found.");
        }

        bool alreadyInvited = await dbContext.NfsShareInvites
            .AnyAsync(invite => invite.RemoteServerId == remoteServerId, cancellationToken);

        if (alreadyInvited)
        {
            throw new InvalidOperationException("This server already has an NFS invitation.");
        }

        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        string inviteKey = GenerateInviteKey();
        string inviteLink = BuildInviteLink(vpnConfig.Endpoint, vpnConfig.ListenPort, inviteKey);

        dbContext.NfsShareInvites.Add(new NfsShareInviteEntity
        {
            RemoteServerId = remoteServer.Id,
            RemoteServer = remoteServer,
            InviteKey = inviteKey,
            InviteLink = inviteLink
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        invitationEventsService.NotifyChanged();
        return inviteLink;
    }

    public async Task<NfsShareInviteResponse> GetShareRequestAsync(string inviteKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inviteKey))
        {
            throw new InvalidOperationException("NFS invite key is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        NfsShareInviteEntity? invite = await dbContext.NfsShareInvites
            .Include(item => item.RemoteServer)
            .FirstOrDefaultAsync(item => item.InviteKey == inviteKey.Trim(), cancellationToken);

        if (invite is null)
        {
            throw new InvalidOperationException("NFS invite key is invalid.");
        }

        if (invite.UsedAtUtc is not null)
        {
            throw new InvalidOperationException("NFS invite key has already been used.");
        }

        invite.UsedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        invitationEventsService.NotifyChanged();

        NfsConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);

        return new NfsShareInviteResponse(
            ClusterShareConstants.ClusterDirectoryPath,
            ClusterShareConstants.ClientMountPath,
            configuration.ClientConfigContent);
    }

    public async Task DeleteInviteAsync(int inviteId, CancellationToken cancellationToken = default)
    {
        if (inviteId <= 0)
        {
            throw new InvalidOperationException("NFS invitation is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        NfsShareInviteEntity invite = await dbContext.NfsShareInvites
            .FirstOrDefaultAsync(item => item.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("NFS invitation was not found.");

        dbContext.NfsShareInvites.Remove(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
        invitationEventsService.NotifyChanged();
    }

    public async Task<string> LoadInvitationConfigAsync(int inviteId, CancellationToken cancellationToken = default)
    {
        if (inviteId <= 0)
        {
            throw new InvalidOperationException("NFS invitation is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await dbContext.NfsShareInvites
            .AnyAsync(item => item.Id == inviteId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("NFS invitation was not found.");
        }

        NfsConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);
        return configuration.ClientConfigContent;
    }

    private static string BuildServerConfig(string configuredAddress)
    {
        string shareSubnet = GetShareSubnet(configuredAddress);

        return Normalize($"""
{ClusterShareConstants.ClusterDirectoryPath} {shareSubnet}(rw,sync,no_subtree_check,no_root_squash)
""");
    }

    private static string BuildClientConfig(string controlVpnIp)
    {
        return Normalize($"""
{controlVpnIp}:{ClusterShareConstants.ClusterDirectoryPath} {ClusterShareConstants.ClientMountPath} nfs defaults,_netdev,nofail,x-systemd.automount,x-systemd.requires=wg-quick@wg0.service 0 0
""");
    }

    private static string GetShareSubnet(string configuredAddress)
    {
        string controlVpnIp = configuredAddress.Split('/', 2, StringSplitOptions.TrimEntries)[0];
        string[] octets = controlVpnIp.Split('.', StringSplitOptions.TrimEntries);
        if (octets.Length == 4)
        {
            return $"{octets[0]}.{octets[1]}.{octets[2]}.0/24";
        }

        return "10.10.10.0/24";
    }

    private static string GenerateInviteKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    private static string BuildInviteLink(string? endpoint, string? listenPort, string inviteKey)
    {
        string host = string.IsNullOrWhiteSpace(endpoint)
            ? throw new InvalidOperationException("VPN endpoint is required before generating an NFS invite link.")
            : NormalizeInviteEndpoint(endpoint.Trim(), listenPort);

        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"{host.TrimEnd('/')}/api/nfs/invite/{inviteKey}";
        }

        return $"https://{host}/api/nfs/invite/{inviteKey}";
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

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }
}
