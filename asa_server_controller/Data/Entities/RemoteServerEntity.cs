namespace asa_server_controller.Data.Entities;

public sealed class RemoteServerEntity : BaseEntity
{
    public required string VpnAddress { get; set; }
    public int? Port { get; set; }
    public required string InviteStatus { get; set; }
    public required string ValidationStatus { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int? MaxPlayers { get; set; }
    public int? GamePort { get; set; }
    public DateTimeOffset? ServerInfoCheckedAtUtc { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public ICollection<InvitationEntity> Invitations { get; set; } = [];
    public ICollection<NfsShareInviteEntity> NfsShareInvites { get; set; } = [];
}
