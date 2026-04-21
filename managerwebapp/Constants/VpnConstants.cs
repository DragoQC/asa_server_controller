namespace managerwebapp.Constants;

public static class VpnConstants
{
    public const string WgPath = "/usr/bin/wg";
    public const string WgQuickPath = "/usr/bin/wg-quick";
    public const string VpnDirectoryPath = "/opt/asa-control/vpn";
    public const string InvitationDirectoryPath = "/opt/asa-control/vpn/invitation";
    public const string VpnConfigFilePath = "/opt/asa-control/vpn/wg0.conf";
    public const string ClientPrivateKeyFilePath = "/opt/asa-control/vpn/client.key";
    public const string ClientPublicKeyFilePath = "/opt/asa-control/vpn/client.pub";
    public const string ServerPrivateKeyFilePath = "/opt/asa-control/vpn/server.key";
    public const string ServerPublicKeyFilePath = "/opt/asa-control/vpn/server.pub";
    public const string WireGuardServiceName = "wg-quick@wg0";

    public const string DefaultAddress = "10.10.10.2/32";
    public const string DefaultListenPort = "51820";
    public const string DefaultAllowedIps = "10.10.10.0/24";
    public const string DefaultPersistentKeepalive = "25";
}
