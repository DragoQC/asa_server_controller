namespace managerwebapp.Models.Servers;

public sealed record ClaimedInvitationOption(
    int InvitationId,
    string VpnAddress,
    string RemoteApiKey,
    DateTimeOffset? UsedAtUtc);
