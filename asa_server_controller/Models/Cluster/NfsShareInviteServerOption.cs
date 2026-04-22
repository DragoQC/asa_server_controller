namespace asa_server_controller.Models.Cluster;

public sealed record NfsShareInviteServerOption(
    int Id,
    string VpnAddress,
    int? Port);
