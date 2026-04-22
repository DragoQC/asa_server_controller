namespace asa_server_controller.Data.Entities;

public sealed class InvitationEntity : BaseEntity
{
    public int RemoteServerId { get; set; }
    public RemoteServerEntity RemoteServer { get; set; } = null!;
    public string RemoteUrl { get; set; } = string.Empty;
    public required string ClusterId { get; set; }
    public required string OneTimeVpnKey { get; set; }
    public required string InviteLink { get; set; }
    public required string InviteStatus { get; set; }
    public required string ValidationStatus { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
}
