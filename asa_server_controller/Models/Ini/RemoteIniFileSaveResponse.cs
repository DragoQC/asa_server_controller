namespace asa_server_controller.Models.Ini;

public sealed record RemoteIniFileSaveResponse(
    bool Success,
    string Message,
    string? Path);
