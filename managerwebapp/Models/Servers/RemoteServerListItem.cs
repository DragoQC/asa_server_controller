namespace managerwebapp.Models.Servers;

public sealed record RemoteServerListItem(
    int Id,
    string VpnAddress,
    int Port,
    string InviteStatus,
    string ValidationStatus,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset CreatedAtUtc);
