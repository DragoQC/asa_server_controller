namespace asa_server_controller.Models.Logs;

public sealed record ControlLogsSnapshot(
    LogSectionSnapshot WireGuardStatusSection,
    DateTimeOffset LoadedAtUtc);
