namespace asa_server_controller.Data.Entities;

public sealed class VpnServerSettingsEntity : BaseEntity
{
    public string Endpoint { get; set; } = string.Empty;
    public string AllowedIps { get; set; } = string.Empty;
    public string PersistentKeepalive { get; set; } = string.Empty;
    public string PresharedKey { get; set; } = string.Empty;
}
