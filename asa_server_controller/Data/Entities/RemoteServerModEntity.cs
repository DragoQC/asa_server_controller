namespace asa_server_controller.Data.Entities;

public sealed class RemoteServerModEntity : BaseEntity
{
    public int RemoteServerId { get; set; }
    public int ModEntityId { get; set; }
}
