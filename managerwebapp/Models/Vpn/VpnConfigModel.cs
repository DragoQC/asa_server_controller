namespace managerwebapp.Models.Vpn;

public sealed class VpnConfigModel
{
    public string? PrivateKey { get; set; }
    public string? Address { get; set; }
    public string? ListenPort { get; set; }
    public string? PeerPublicKey { get; set; }
    public string? PresharedKey { get; set; }
    public string? Endpoint { get; set; }
    public string? AllowedIps { get; set; }
    public string? PersistentKeepalive { get; set; }
}
