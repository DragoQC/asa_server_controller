namespace asa_server_controller.Models.Invitations;

public sealed class VpnInviteFormModel
{
    public bool IsReady { get; init; }
    public bool IsVpnInstalled { get; init; }
    public string? StatusMessage { get; init; }
    public string ClusterId { get; init; } = string.Empty;
    public string? VpnAddress { get; init; }
    public string Port { get; init; } = string.Empty;
}
