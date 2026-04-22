namespace asa_server_controller.Models.Vpn;

public sealed class VpnServerSettingsModel
{
    public string? Endpoint { get; set; }
    public string? AllowedIps { get; set; }
    public string? PersistentKeepalive { get; set; }
    public string? PresharedKey { get; set; }
}
