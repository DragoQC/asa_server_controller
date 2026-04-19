namespace managerwebapp.Models.Servers;

public sealed record RemoteServerListItem(
    int Id,
    string VpnAddress,
    int? Port,
    string StateLabel,
    bool CanStart,
    bool CanStop,
    bool CanOpenRcon,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset CreatedAtUtc);
