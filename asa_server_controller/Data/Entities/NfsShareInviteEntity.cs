namespace asa_server_controller.Data.Entities;

public sealed class NfsShareInviteEntity : BaseEntity
{
    public int RemoteServerId { get; set; }
    public RemoteServerEntity RemoteServer { get; set; } = null!;
    public required string InviteKey { get; set; }
    public required string InviteLink { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
}
