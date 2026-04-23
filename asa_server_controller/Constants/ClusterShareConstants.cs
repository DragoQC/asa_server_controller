namespace asa_server_controller.Constants;

public static class ClusterShareConstants
{
    public const string ClusterDirectoryPath = "/opt/asa-control/cluster";
    public const string NfsDirectoryPath = "/opt/asa-control/nfs";
    public const string ServerConfigFilePath = NfsDirectoryPath + "/exports.conf";
    public const string ClientConfigFilePath = NfsDirectoryPath + "/client.mount.conf";
    public const string ApplyServerScriptPath = NfsDirectoryPath + "/apply-nfs-server.sh";
    public const string SystemExportsFilePath = "/etc/exports";
    public const string NfsServiceName = "nfs-server";
    public const string ClientMountPath = "/opt/asa/cluster";
}
