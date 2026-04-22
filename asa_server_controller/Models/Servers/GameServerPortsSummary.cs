namespace asa_server_controller.Models.Servers;

public sealed record GameServerPortsSummary(
    IReadOnlyList<GameServerPortInfo> Servers,
    IReadOnlyList<string> MissingServerPorts);
