namespace asa_server_controller.Models.Home;

public sealed record ServerDisplay(
    IReadOnlyList<HomeServerModel> Servers,
    bool HasCurseForgeApiKey);
