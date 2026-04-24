namespace asa_server_controller.Services;

public sealed record RemoteApiResult<T>(
    bool Success,
    T? Data,
    string? Message);
