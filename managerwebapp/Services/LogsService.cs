using System.Diagnostics;
using managerwebapp.Constants;
using managerwebapp.Models.Logs;

namespace managerwebapp.Services;

public sealed class LogsService
{
    public async Task<ControlLogsSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult statusResult = await RunProcessAsync(
            GlobalConstants.SudoPath,
            [
                "-n",
                GlobalConstants.SystemctlPath,
                "show",
                GlobalConstants.ControlWebAppServiceName,
                "--no-pager",
                "--property=Id,LoadState,ActiveState,SubState,UnitFileState,MainPID,ExecMainStatus,ExecMainStartTimestamp,FragmentPath"
            ],
            cancellationToken,
            throwOnNonZero: false);

        ProcessResult wireGuardStatusResult = await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "status", VpnConstants.WireGuardServiceName, "--no-pager", "--full"],
            cancellationToken,
            throwOnNonZero: false);

        ProcessResult journalResult = await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.JournalctlPath, "-u", GlobalConstants.ControlWebAppServiceName, "-t", "dotnet", "-n", "100", "--no-pager", "-o", "cat"],
            cancellationToken,
            throwOnNonZero: false);

        string statusContent = GetContentOrUnavailable(statusResult.Output);
        string wireGuardStatusContent = GetContentOrUnavailable(wireGuardStatusResult.Output);
        string journalContent = GetContentOrUnavailable(journalResult.Output);

        return new ControlLogsSnapshot(
            new LogSectionSnapshot(
                "Service status",
                $"Live systemctl status output for {GlobalConstants.ControlWebAppServiceName}.",
                statusContent,
                !IsUnavailable(statusContent)),
            new LogSectionSnapshot(
                "WireGuard status",
                $"Live systemctl status output for {VpnConstants.WireGuardServiceName}.",
                wireGuardStatusContent,
                !IsUnavailable(wireGuardStatusContent)),
            new LogSectionSnapshot(
                "App journal",
                $"Recent journalctl output for {GlobalConstants.ControlWebAppServiceName}.",
                journalContent,
                !IsUnavailable(journalContent)),
            DateTimeOffset.UtcNow);
    }

    private static string GetContentOrUnavailable(string output)
    {
        return string.IsNullOrWhiteSpace(output)
            ? "Service unavailable or not present."
            : output.TrimEnd();
    }

    private static bool IsUnavailable(string value)
    {
        return string.Equals(value, "Service unavailable or not present.", StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool throwOnNonZero = true)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        string combinedOutput = string.IsNullOrWhiteSpace(stdout)
            ? stderr
            : string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : $"{stdout}\n{stderr}";

        if (process.ExitCode == 0 || !throwOnNonZero)
        {
            return new ProcessResult(process.ExitCode, combinedOutput);
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(combinedOutput)
            ? "System command failed."
            : combinedOutput.Trim());
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
