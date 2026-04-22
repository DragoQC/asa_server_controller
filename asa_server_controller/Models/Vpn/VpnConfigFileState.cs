namespace asa_server_controller.Models.Vpn;

public sealed record VpnConfigFileState(
    string Title,
    string Description,
    string FilePath,
    string StateLabel,
    bool Exists);
