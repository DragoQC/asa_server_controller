namespace asa_server_controller.Models.Cluster;

public sealed record NfsShareInviteResponse(
    string SharePath,
    string MountPath,
    string ClientConfig);
