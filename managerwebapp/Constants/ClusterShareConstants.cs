namespace managerwebapp.Constants;

public static class ClusterShareConstants
{
    public const string ClusterDirectoryPath = "/opt/asa-control/cluster";
    public const string NfsDirectoryPath = "/opt/asa-control/nfs";
    public const string ServerConfigFilePath = NfsDirectoryPath + "/exports.conf";
    public const string ClientConfigFilePath = NfsDirectoryPath + "/client.mount.conf";
    public const string ClientMountPath = "/opt/asa/cluster";
}
