namespace asa_server_controller.Models.Ui;

public sealed record ToastItem(
    Guid Id,
    ToastLevel Level,
    string Tag,
    string Message,
    DateTimeOffset CreatedAtUtc);
