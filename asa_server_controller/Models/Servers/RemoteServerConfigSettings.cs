namespace asa_server_controller.Models.Servers;

public sealed class RemoteServerConfigSettings
{
    public string MapName { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public int MaxPlayers { get; set; }

    public int GamePort { get; set; }

    public int QueryPort { get; set; }

    public int RconPort { get; set; }

    public string ModIds { get; set; } = string.Empty;

    public string ClusterId { get; set; } = string.Empty;

    public string ClusterDir { get; set; } = string.Empty;

    public string CustomExtraArgs { get; set; } = string.Empty;
}
