using System.Diagnostics;
using asa_server_controller.Constants;

namespace asa_server_controller.Services;

public sealed class SudoService
{
    public async Task<string> InstallWireGuardAsync(CancellationToken cancellationToken = default)
    {
        await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.PrepareClusterServerScriptPath],
            cancellationToken);

        return "Cluster server tools installed.";
    }

    public Task RestartWireGuardAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "restart", VpnConstants.WireGuardServiceName],
            cancellationToken);
    }

    public Task EnableWireGuardAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SystemctlPath, "enable", VpnConstants.WireGuardServiceName],
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

    public async Task<string> ApplyNfsServerAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", ClusterShareConstants.ApplyServerScriptPath],
            cancellationToken,
            throwOnNonZero: false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output)
                ? "NFS apply failed."
                : result.Output);
        }

        return string.IsNullOrWhiteSpace(result.Output)
            ? $"{ClusterShareConstants.NfsServiceName} applied."
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
