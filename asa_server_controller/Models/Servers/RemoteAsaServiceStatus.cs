namespace asa_server_controller.Models.Servers;

public sealed record RemoteAsaServiceStatus(
    string ActiveState,
    string SubState,
    string Result,
    string UnitFileState,
    string DisplayText,
    bool CanStart,
    bool CanStop,
    DateTimeOffset? ActiveSinceUtc,
    string UptimeText)
{
    public bool IsRunning => string.Equals(ActiveState, "active", StringComparison.Ordinal);

    public bool IsStarting => string.Equals(ActiveState, "activating", StringComparison.Ordinal);

    public bool IsStopping => string.Equals(ActiveState, "deactivating", StringComparison.Ordinal);

    public bool IsStopped => string.Equals(ActiveState, "inactive", StringComparison.Ordinal);

    public bool IsFailed => string.Equals(ActiveState, "failed", StringComparison.Ordinal);

    public bool IsUnavailable => string.Equals(ActiveState, "unknown", StringComparison.Ordinal);

    public bool IsUpOrStarting => IsRunning || IsStarting;

    public static RemoteAsaServiceStatus Unknown(string displayText = "Unknown")
    {
        return new RemoteAsaServiceStatus("unknown", "unknown", "unknown", "unknown", displayText, false, false, null, "Unavailable");
    }
}
