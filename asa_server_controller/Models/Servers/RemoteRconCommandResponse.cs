namespace asa_server_controller.Models.Servers;

public sealed record RemoteRconCommandResponse(
    bool Success,
    string? Command,
    string? Response,
    string? State,
    string? Message);
