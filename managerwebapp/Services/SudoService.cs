using System.Diagnostics;
using managerwebapp.Constants;

namespace managerwebapp.Services;

public sealed class SudoService
{
    public async Task<string> InstallWireGuardAsync(CancellationToken cancellationToken = default)
    {
        await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.PrepareWireGuardServerScriptPath],
            cancellationToken);

        return "WireGuard server tools installed.";
    }

    public Task RestartWireGuardAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "restart", VpnConstants.WireGuardServiceName],
            cancellationToken);
    }

    public Task StartWireGuardAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "start", VpnConstants.WireGuardServiceName],
            cancellationToken);
    }

    public Task StopWireGuardAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "stop", VpnConstants.WireGuardServiceName],
            cancellationToken);
    }

    public async Task<bool> IsWireGuardActiveAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "is-active", VpnConstants.WireGuardServiceName, "--quiet"],
            cancellationToken,
            throwOnNonZero: false);

        return result.ExitCode == 0;
    }

    public async Task<string> GetWireGuardStatusAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "status", VpnConstants.WireGuardServiceName, "--no-pager", "--full"],
            cancellationToken,
            throwOnNonZero: false);

        return string.IsNullOrWhiteSpace(result.Output)
            ? $"No status output for {VpnConstants.WireGuardServiceName}."
            : result.Output;
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
                UseShellExecute = false
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

        if (process.ExitCode == 0)
        {
            return new ProcessResult(process.ExitCode, stdout.Trim());
        }

        string combinedOutput = string.Join(
            Environment.NewLine,
            new[] { stdout.Trim(), stderr.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (throwOnNonZero)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(combinedOutput)
                ? "System command failed."
                : combinedOutput);
        }

        return new ProcessResult(process.ExitCode, combinedOutput);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
