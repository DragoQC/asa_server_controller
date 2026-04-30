namespace asa_server_controller.Constants;

public static class GlobalConstants
{
    public const string SudoPath = "/usr/bin/sudo";
    public const string IptablesPath = "/usr/sbin/iptables";
    public const string SysctlPath = "/usr/sbin/sysctl";
    public const string SystemctlPath = "/usr/bin/systemctl";
    public const string JournalctlPath = "/usr/bin/journalctl";
    public const string ControlWebAppServiceName = "asa-webapp";
    public const string PrepareClusterServerScriptPath = "/opt/asa-control/vpn/prepare-cluster-server.sh";
}
