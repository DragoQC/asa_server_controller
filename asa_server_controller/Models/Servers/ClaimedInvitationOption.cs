namespace asa_server_controller.Models.Servers;

public sealed record ClaimedInvitationOption(
    int InvitationId,
    string VpnAddress,
    string RemoteApiKey,
    DateTimeOffset? UsedAtUtc);
