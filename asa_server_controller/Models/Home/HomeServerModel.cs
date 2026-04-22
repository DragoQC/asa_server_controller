using asa_server_controller.Models.Servers;

namespace asa_server_controller.Models.Home;

public sealed record HomeServerModel(
    int RemoteServerId,
    string DisplayServerName,
    string StateLabel,
    int CurrentPlayers,
    int MaxPlayers,
    string FormattedMapName,
    IReadOnlyList<PublicServerModItem> Mods);
