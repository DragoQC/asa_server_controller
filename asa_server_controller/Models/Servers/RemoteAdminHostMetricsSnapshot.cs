namespace asa_server_controller.Models.Servers;

public sealed record RemoteAdminHostMetricsSnapshot(
    string CpuUsage,
    string RamUsage,
    string RamUsed,
    string DiskUsage,
    string DiskUsed,
    DateTimeOffset CheckedAtUtc,
    int RemoteServerId = 0,
    string ConnectionState = "Disconnected")
{
    public static RemoteAdminHostMetricsSnapshot Default(int remoteServerId)
    {
        return new RemoteAdminHostMetricsSnapshot(
            "0%",
            "0%",
            "0 B",
            "0%",
            "0 B",
            DateTimeOffset.UtcNow,
            remoteServerId,
            "Disconnected");
    }
}
