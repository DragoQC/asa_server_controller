namespace managerwebapp.Constants;

public static class VpnConstants
{
    public const string WgPath = "/usr/bin/wg";
    public const string WgQuickPath = "/usr/bin/wg-quick";
    public const string VpnDirectoryPath = "/opt/asa-control/vpn";
    public const string VpnConfigFilePath = "/opt/asa-control/vpn/wg0.conf";
    public const string ClientPrivateKeyFilePath = "/opt/asa-control/vpn/client.key";
    public const string ClientPublicKeyFilePath = "/opt/asa-control/vpn/client.pub";
    public const string ServerPrivateKeyFilePath = "/opt/asa-control/vpn/server.key";
    public const string ServerPublicKeyFilePath = "/opt/asa-control/vpn/server.pub";
    public const string WireGuardServiceName = "wg-quick@wg0";
}
