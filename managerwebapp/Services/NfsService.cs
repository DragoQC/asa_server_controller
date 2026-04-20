using managerwebapp.Constants;
using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Cluster;
using managerwebapp.Models.Vpn;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace managerwebapp.Services;

public sealed class NfsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    VpnService vpnService)
{
    public async Task<NfsConfigurationModel> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        string configuredAddress = await vpnService.LoadCurrentAddressAsync(cancellationToken);
        string configuredIpAddress = await vpnService.LoadCurrentIpAddressAsync(cancellationToken);

        bool clusterFolderExists = Directory.Exists(ClusterShareConstants.ClusterDirectoryPath);
        bool serverConfigExists = File.Exists(ClusterShareConstants.ServerConfigFilePath);
        bool clientConfigExists = File.Exists(ClusterShareConstants.ClientConfigFilePath);

        string serverConfigContent = serverConfigExists
            ? File.ReadAllText(ClusterShareConstants.ServerConfigFilePath)
            : BuildServerConfig(configuredAddress);

        string clientConfigContent = clientConfigExists
            ? File.ReadAllText(ClusterShareConstants.ClientConfigFilePath)
            : BuildClientConfig(configuredIpAddress);

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
        }

        if (File.Exists(ClusterShareConstants.ClientConfigFilePath))
        {
            await File.WriteAllTextAsync(
                ClusterShareConstants.ClientConfigFilePath,
                BuildClientConfig(configuredIpAddress),
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<NfsShareInviteServerOption>> LoadTargetServersAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.RemoteServers
            .Where(server => server.InviteStatus == "Accepted")
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

    public async Task<NfsShareInviteResponse> BuildPreviewAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
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

    public async Task<string> CreateInviteLinkAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
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

        NfsConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);
        return new NfsShareInviteResponse(
            ClusterShareConstants.ClusterDirectoryPath,
            ClusterShareConstants.ClientMountPath,
            configuration.ClientConfigContent);
    }

    private static string BuildServerConfig(string configuredAddress)
    {
        string shareSubnet = GetShareSubnet(configuredAddress);

        return Normalize($"""
# NFS export for the ASA cluster share.
# Apply this to /etc/exports when you are ready to expose the share to the VPN subnet.
{ClusterShareConstants.ClusterDirectoryPath} {shareSubnet}(rw,sync,no_subtree_check,no_root_squash)
""");
    }

    private static string BuildClientConfig(string controlVpnIp)
    {
        return Normalize($"""
# Client mount example for a remote ASA node.
# Add this line to /etc/fstab on the node when automatic mount support is ready there.
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
