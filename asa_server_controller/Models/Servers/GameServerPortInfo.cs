namespace asa_server_controller.Models.Servers;

public sealed record GameServerPortInfo(
    int RemoteServerId,
    string Name,
    string VpnAddress,
    int? ExposedGamePort,
    int? GamePort);
