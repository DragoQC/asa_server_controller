namespace managerwebapp.Models.Servers;

public sealed record GameServerPortInfo(
    int RemoteServerId,
    string Name,
    string VpnAddress,
    int? GamePort);
