namespace asa_server_controller.Models.Servers;

public sealed record RemoteManagerCommandResponse(
    bool Success,
    string Message,
    string State);
