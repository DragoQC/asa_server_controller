namespace asa_server_controller.Models.Servers;

public sealed record RemoteServerListItem(
    int Id,
    string ServerName,
    string VpnAddress,
    int? Port,
    string StateLabel,
    bool IsOnline,
    bool CanStart,
    bool CanStop,
    bool CanSendRconCommand,
    string MapName,
    int CurrentPlayers,
    int MaxPlayers,
    DateTimeOffset? ServerInfoCheckedAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset CreatedAtUtc);
