using System.Security.Cryptography;
using asa_server_controller.Constants;
using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Cluster;
using asa_server_controller.Models.Vpn;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class SmbService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    VpnService vpnService,
    SudoService sudoService,
    InvitationEventsService invitationEventsService)
{
    public async Task<SmbConfigurationModel> LoadConfigurationAsync(CancellationToken cancellationToken = default)
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

        return new SmbConfigurationModel(
            clusterFolderExists,
            serverConfigExists,
            clientConfigExists,
            ClusterShareConstants.ClusterDirectoryPath,
            ClusterShareConstants.ServerConfigFilePath,
            ClusterShareConstants.ClientConfigFilePath,
            Normalize(serverConfigContent),
            Normalize(clientConfigContent));
    }

    public async Task<SmbConfigurationModel> CreateDefaultConfigAsync(CancellationToken cancellationToken = default)
    {
        string configuredAddress = await vpnService.LoadConfiguredAddressAsync(cancellationToken);
        string configuredIpAddress = await vpnService.LoadConfiguredIpAddressAsync(cancellationToken);

        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.SmbDirectoryPath);

        string serverConfig = BuildServerConfig(configuredAddress);
        string clientConfig = BuildClientConfig(configuredIpAddress);

        await File.WriteAllTextAsync(ClusterShareConstants.ServerConfigFilePath, serverConfig, cancellationToken);
        await File.WriteAllTextAsync(ClusterShareConstants.ClientConfigFilePath, clientConfig, cancellationToken);
        await sudoService.ApplySmbServerAsync(cancellationToken);

        return await LoadConfigurationAsync(cancellationToken);
    }

    public async Task<SmbConfigurationModel> SaveConfigurationAsync(string serverConfigContent, string clientConfigContent, CancellationToken cancellationToken = default)
    {
        await vpnService.LoadConfiguredAddressAsync(cancellationToken);

        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.SmbDirectoryPath);

        await File.WriteAllTextAsync(ClusterShareConstants.ServerConfigFilePath, Normalize(serverConfigContent), cancellationToken);
        await File.WriteAllTextAsync(ClusterShareConstants.ClientConfigFilePath, Normalize(clientConfigContent), cancellationToken);
        await sudoService.ApplySmbServerAsync(cancellationToken);

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
        Directory.CreateDirectory(ClusterShareConstants.SmbDirectoryPath);

        if (File.Exists(ClusterShareConstants.ServerConfigFilePath))
        {
            await File.WriteAllTextAsync(
                ClusterShareConstants.ServerConfigFilePath,
                BuildServerConfig(configuredAddress),
                cancellationToken);
            await sudoService.ApplySmbServerAsync(cancellationToken);
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

    public async Task<SmbInviteFormModel> LoadSmbFormAsync(CancellationToken cancellationToken = default)
    {
        if (!await vpnService.IsConfiguredAsync(cancellationToken))
        {
            return new SmbInviteFormModel
            {
                IsReady = false,
                StatusMessage = "Finish the VPN setup on the Cluster setup page before creating SMB invitations."
            };
        }

        if (!await IsConfiguredAsync(cancellationToken))
        {
            return new SmbInviteFormModel
            {
                IsReady = false,
                StatusMessage = "Save the SMB configuration on the Cluster setup page before creating SMB invitations."
            };
        }

        return new SmbInviteFormModel
        {
            IsReady = true
        };
    }

    public async Task<IReadOnlyList<SmbShareInviteServerOption>> LoadTargetServersAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.RemoteServers
            .Where(server => server.InviteStatus == "Accepted")
            .OrderBy(server => server.VpnAddress)
            .Select(server => new SmbShareInviteServerOption(
                server.Id,
                server.VpnAddress,
                server.Port))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SmbShareInviteListItem>> LoadInvitesAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<SmbShareInviteListItem> items = await dbContext.SmbShareInvites
            .Select(invite => new SmbShareInviteListItem(
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

    public async Task<SmbShareInviteResponse> BuildSmbPreviewAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        SmbInviteFormModel form = await LoadSmbFormAsync(cancellationToken);
        if (!form.IsReady)
        {
            throw new InvalidOperationException(form.StatusMessage ?? "SMB invitations are not ready.");
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
            throw new InvalidOperationException("Accepted remote server is required for an SMB invite.");
        }

        string controlVpnIp = await vpnService.LoadConfiguredIpAddressAsync(cancellationToken);
        SmbConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);

        return new SmbShareInviteResponse(
            BuildSharePath(controlVpnIp),
            ClusterShareConstants.ClientMountPath,
            configuration.ClientConfigContent);
    }

    public async Task<string> CreateSmbInvitationLinkAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        SmbInviteFormModel form = await LoadSmbFormAsync(cancellationToken);
        if (!form.IsReady)
        {
            throw new InvalidOperationException(form.StatusMessage ?? "SMB invitations are not ready.");
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

        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        string inviteKey = GenerateInviteKey();
        string inviteLink = BuildInviteLink(vpnConfig.Endpoint, vpnConfig.ListenPort, inviteKey);

        dbContext.SmbShareInvites.Add(new SmbShareInviteEntity
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

    public async Task<SmbShareInviteResponse> GetShareRequestAsync(string inviteKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inviteKey))
        {
            throw new InvalidOperationException("SMB invite key is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        SmbShareInviteEntity? invite = await dbContext.SmbShareInvites
            .Include(item => item.RemoteServer)
            .FirstOrDefaultAsync(item => item.InviteKey == inviteKey.Trim(), cancellationToken);

        if (invite is null)
        {
            throw new InvalidOperationException("SMB invite key is invalid.");
        }

        if (invite.UsedAtUtc is not null)
        {
            throw new InvalidOperationException("SMB invite key has already been used.");
        }

        invite.UsedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        invitationEventsService.NotifyChanged();

        string controlVpnIp = await vpnService.LoadConfiguredIpAddressAsync(cancellationToken);
        SmbConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);

        return new SmbShareInviteResponse(
            BuildSharePath(controlVpnIp),
            ClusterShareConstants.ClientMountPath,
            configuration.ClientConfigContent);
    }

    public async Task DeleteInviteAsync(int inviteId, CancellationToken cancellationToken = default)
    {
        if (inviteId <= 0)
        {
            throw new InvalidOperationException("SMB invitation is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        SmbShareInviteEntity invite = await dbContext.SmbShareInvites
            .FirstOrDefaultAsync(item => item.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("SMB invitation was not found.");

        dbContext.SmbShareInvites.Remove(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
        invitationEventsService.NotifyChanged();
    }

    public async Task<string> LoadInvitationConfigAsync(int inviteId, CancellationToken cancellationToken = default)
    {
        if (inviteId <= 0)
        {
            throw new InvalidOperationException("SMB invitation is required.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await dbContext.SmbShareInvites
            .AnyAsync(item => item.Id == inviteId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("SMB invitation was not found.");
        }

        SmbConfigurationModel configuration = await LoadConfigurationAsync(cancellationToken);
        return configuration.ClientConfigContent;
    }

    private static string BuildServerConfig(string configuredAddress)
    {
        string shareSubnet = GetShareSubnet(configuredAddress);

        return Normalize($"""
[global]
   server role = standalone server
   workgroup = WORKGROUP
   map to guest = Bad User
   guest account = nobody
   server min protocol = SMB3
   disable netbios = yes
   hosts allow = {shareSubnet} 127.0.0.1

[{ClusterShareConstants.ShareName}]
   path = {ClusterShareConstants.ClusterDirectoryPath}
   browseable = yes
   guest ok = yes
   guest only = yes
   read only = no
   force user = asa_manager_web_app
   force group = asa_manager_web_app
   create mask = 0664
   directory mask = 0775
""");
    }

    private static string BuildClientConfig(string controlVpnIp)
    {
        return Normalize($"""
{BuildSharePath(controlVpnIp)} {ClusterShareConstants.ClientMountPath} cifs guest,vers=3.1.1,iocharset=utf8,_netdev,nofail,x-systemd.automount,x-systemd.requires=wg-quick@wg0.service,file_mode=0664,dir_mode=0775 0 0
""");
    }

    private static string BuildSharePath(string controlVpnIp)
    {
        return $"//{controlVpnIp}/{ClusterShareConstants.ShareName}";
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
            ? throw new InvalidOperationException("VPN endpoint is required before generating an SMB invite link.")
            : NormalizeInviteEndpoint(endpoint.Trim(), listenPort);

        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"{host.TrimEnd('/')}/api/smb/invite/{inviteKey}";
        }

        return $"https://{host}/api/smb/invite/{inviteKey}";
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
