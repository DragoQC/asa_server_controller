namespace asa_server_controller.Models.Logs;

public sealed record LogSectionSnapshot(
    string Title,
    string Description,
    string Content,
    bool IsAvailable);
