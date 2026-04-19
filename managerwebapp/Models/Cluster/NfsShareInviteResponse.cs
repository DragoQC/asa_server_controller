namespace managerwebapp.Models.Cluster;

public sealed record NfsShareInviteResponse(
    string SharePath,
    string MountPath,
    string ClientConfig);
