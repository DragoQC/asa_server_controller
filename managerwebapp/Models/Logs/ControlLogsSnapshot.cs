namespace managerwebapp.Models.Logs;

public sealed record ControlLogsSnapshot(
    LogSectionSnapshot WireGuardStatusSection,
    DateTimeOffset LoadedAtUtc);
