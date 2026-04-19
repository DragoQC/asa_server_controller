using managerwebapp.Constants;
using managerwebapp.Models.Cluster;

namespace managerwebapp.Services;

public sealed class NfsConfigurationService(VpnConfigService vpnConfigService)
{
    public async Task<NfsConfigurationModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        string configuredAddress = await vpnConfigService.LoadConfiguredAddressAsync(cancellationToken);
        string configuredIpAddress = await vpnConfigService.LoadConfiguredIpAddressAsync(cancellationToken);

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
        string configuredAddress = await vpnConfigService.LoadConfiguredAddressAsync(cancellationToken);
        string configuredIpAddress = await vpnConfigService.LoadConfiguredIpAddressAsync(cancellationToken);

        Directory.CreateDirectory(ClusterShareConstants.ClusterDirectoryPath);
        Directory.CreateDirectory(ClusterShareConstants.NfsDirectoryPath);

        string serverConfig = BuildServerConfig(configuredAddress);
        string clientConfig = BuildClientConfig(configuredIpAddress);

        await File.WriteAllTextAsync(ClusterShareConstants.ServerConfigFilePath, serverConfig, cancellationToken);
        await File.WriteAllTextAsync(ClusterShareConstants.ClientConfigFilePath, clientConfig, cancellationToken);

        return await LoadAsync(cancellationToken);
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

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }
}
