using managerwebapp.Constants;
using managerwebapp.Models.Cluster;
using managerwebapp.Models.Vpn;

namespace managerwebapp.Services;

public sealed class NfsConfigurationService
{
    public Task<NfsConfigurationModel> LoadAsync(VpnConfigModel vpnConfig, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool clusterFolderExists = Directory.Exists(ClusterShareConstants.ClusterDirectoryPath);
        bool serverConfigExists = File.Exists(ClusterShareConstants.ServerConfigFilePath);
        bool clientConfigExists = File.Exists(ClusterShareConstants.ClientConfigFilePath);

        string serverConfigContent = serverConfigExists
            ? File.ReadAllText(ClusterShareConstants.ServerConfigFilePath)
            : BuildServerConfig(vpnConfig);

        string clientConfigContent = clientConfigExists
            ? File.ReadAllText(ClusterShareConstants.ClientConfigFilePath)
            : BuildClientConfig(vpnConfig);

        return Task.FromResult(new NfsConfigurationModel(
            clusterFolderExists,
            serverConfigExists,
            clientConfigExists,
            ClusterShareConstants.ClusterDirectoryPath,
            ClusterShareConstants.ServerConfigFilePath,
            ClusterShareConstants.ClientConfigFilePath,
            Normalize(serverConfigContent),
            Normalize(clientConfigContent)));
    }

    public async Task<NfsConfigurationModel> CreateDefaultConfigAsync(VpnConfigModel vpnConfig, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.NfsDirectoryPath);

        string serverConfig = BuildServerConfig(vpnConfig);
        string clientConfig = BuildClientConfig(vpnConfig);

        await File.WriteAllTextAsync(ClusterShareConstants.ServerConfigFilePath, serverConfig, cancellationToken);
        await File.WriteAllTextAsync(ClusterShareConstants.ClientConfigFilePath, clientConfig, cancellationToken);

        return await LoadAsync(vpnConfig, cancellationToken);
    }

    private static string BuildServerConfig(VpnConfigModel vpnConfig)
    {
        string shareSubnet = GetShareSubnet(vpnConfig);

        return Normalize($"""
# NFS export for the ASA cluster share.
# Apply this to /etc/exports when you are ready to expose the share to the VPN subnet.
{ClusterShareConstants.ClusterDirectoryPath} {shareSubnet}(rw,sync,no_subtree_check,no_root_squash)
""");
    }

    private static string BuildClientConfig(VpnConfigModel vpnConfig)
    {
        string controlVpnIp = GetControlVpnIp(vpnConfig);

        return Normalize($"""
# Client mount example for a remote ASA node.
# Add this line to /etc/fstab on the node when automatic mount support is ready there.
{controlVpnIp}:{ClusterShareConstants.ClusterDirectoryPath} {ClusterShareConstants.ClientMountPath} nfs defaults,_netdev,nofail,x-systemd.automount,x-systemd.requires=wg-quick@wg0.service 0 0
""");
    }

    private static string GetControlVpnIp(VpnConfigModel vpnConfig)
    {
        if (string.IsNullOrWhiteSpace(vpnConfig.Address))
        {
            return "10.10.10.2";
        }

        return vpnConfig.Address.Split('/', 2, StringSplitOptions.TrimEntries)[0];
    }

    private static string GetShareSubnet(VpnConfigModel vpnConfig)
    {
        if (!string.IsNullOrWhiteSpace(vpnConfig.AllowedIps))
        {
            return vpnConfig.AllowedIps.Trim();
        }

        string controlVpnIp = GetControlVpnIp(vpnConfig);
        string[] octets = controlVpnIp.Split('.', StringSplitOptions.TrimEntries);
        if (octets.Length == 4)
        {
            return $"{octets[0]}.{octets[1]}.{octets[2]}.0/24";
        }

        return "10.10.10.0/24";
    }

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }
}
