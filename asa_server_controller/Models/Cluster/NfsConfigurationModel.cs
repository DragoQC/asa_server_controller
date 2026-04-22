namespace asa_server_controller.Models.Cluster;

public sealed record NfsConfigurationModel(
    bool ClusterFolderExists,
    bool ServerConfigExists,
    bool ClientConfigExists,
    string ClusterDirectoryPath,
    string ServerConfigFilePath,
    string ClientConfigFilePath,
    string ServerConfigContent,
    string ClientConfigContent);
