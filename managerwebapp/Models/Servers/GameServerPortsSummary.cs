namespace managerwebapp.Models.Servers;

public sealed record GameServerPortsSummary(
    IReadOnlyList<GameServerPortInfo> Servers,
    IReadOnlyList<string> MissingServerPorts);
