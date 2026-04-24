namespace asa_server_controller.Models.Ini;

public sealed record RemoteIniFileResponse(
    bool Success,
    string? FileName,
    string? Path,
    string? Content,
    string? Message);
