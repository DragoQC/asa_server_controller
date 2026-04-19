namespace managerwebapp.Models.Invitations;

public sealed record InvitationListItem(
    int Id,
    int RemoteServerId,
    string RemoteUrl,
    string ClusterId,
    string VpnAddress,
    string InviteLink,
    string InviteStatus,
    string ValidationStatus,
    DateTimeOffset? UsedAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset CreatedAtUtc);
