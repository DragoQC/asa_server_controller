using System.Diagnostics;
using asa_server_controller.Constants;

namespace asa_server_controller.Services;

public sealed class SudoService
{
    private const string GamePortDnatChain = "ASA_GAME_PORT_DNAT";
    private const string GamePortSnatChain = "ASA_GAME_PORT_SNAT";
    private const string GamePortForwardChain = "ASA_GAME_PORT_FORWARD";

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

    public async Task ApplyGamePortForwardingRulesAsync(
        IReadOnlyList<GamePortForwardingRule> rules,
        CancellationToken cancellationToken = default)
    {
        await RunProcessAsync(
            GlobalConstants.SudoPath,
            ["-n", GlobalConstants.SysctlPath, "-w", "net.ipv4.ip_forward=1"],
            cancellationToken);

        await EnsureChainAsync("nat", GamePortDnatChain, cancellationToken);
        await EnsureJumpAsync("nat", "PREROUTING", ["-j", GamePortDnatChain], cancellationToken);
        await FlushChainAsync("nat", GamePortDnatChain, cancellationToken);

        await EnsureChainAsync("nat", GamePortSnatChain, cancellationToken);
        await EnsureJumpAsync("nat", "POSTROUTING", ["-o", VpnConstants.WireGuardInterfaceName, "-j", GamePortSnatChain], cancellationToken);
        await FlushChainAsync("nat", GamePortSnatChain, cancellationToken);

        await EnsureChainAsync("filter", GamePortForwardChain, cancellationToken);
        await EnsureJumpAsync("filter", "FORWARD", ["-j", GamePortForwardChain], cancellationToken);
        await FlushChainAsync("filter", GamePortForwardChain, cancellationToken);

        foreach (GamePortForwardingRule rule in rules)
        {
            string destination = $"{rule.TargetHost}:{rule.TargetGamePort}";
            await RunIptablesAsync(
                ["-t", "nat", "-A", GamePortDnatChain, "-p", "udp", "--dport", rule.ExposedGamePort.ToString(), "-j", "DNAT", "--to-destination", destination],
                cancellationToken);

            await RunIptablesAsync(
                ["-t", "nat", "-A", GamePortSnatChain, "-p", "udp", "-d", rule.TargetHost, "--dport", rule.TargetGamePort.ToString(), "-j", "MASQUERADE"],
                cancellationToken);

            await RunIptablesAsync(
                ["-A", GamePortForwardChain, "-p", "udp", "-d", rule.TargetHost, "--dport", rule.TargetGamePort.ToString(), "-j", "ACCEPT"],
                cancellationToken);

            await RunIptablesAsync(
                ["-A", GamePortForwardChain, "-p", "udp", "-s", rule.TargetHost, "--sport", rule.TargetGamePort.ToString(), "-m", "conntrack", "--ctstate", "ESTABLISHED,RELATED", "-j", "ACCEPT"],
                cancellationToken);
        }
    }

    private static async Task EnsureChainAsync(string table, string chainName, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunIptablesAsync(
            ["-t", table, "-N", chainName],
            cancellationToken,
            throwOnNonZero: false);

        if (result.ExitCode == 0 || result.Output.Contains("Chain already exists", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "Unable to create iptables chain." : result.Output);
    }

    private static async Task EnsureJumpAsync(
        string table,
        string chainName,
        IReadOnlyList<string> jumpArguments,
        CancellationToken cancellationToken)
    {
        List<string> checkArguments = ["-t", table, "-C", chainName];
        checkArguments.AddRange(jumpArguments);

        ProcessResult existsResult = await RunIptablesAsync(checkArguments, cancellationToken, throwOnNonZero: false);
        if (existsResult.ExitCode == 0)
        {
            return;
        }

        List<string> addArguments = ["-t", table, "-A", chainName];
        addArguments.AddRange(jumpArguments);
        await RunIptablesAsync(addArguments, cancellationToken);
    }

    private static Task FlushChainAsync(string table, string chainName, CancellationToken cancellationToken)
    {
        return RunIptablesAsync(["-t", table, "-F", chainName], cancellationToken);
    }

    private static Task<ProcessResult> RunIptablesAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool throwOnNonZero = true)
    {
        List<string> fullArguments = ["-n", GlobalConstants.IptablesPath];
        fullArguments.AddRange(arguments);
        return RunProcessAsync(GlobalConstants.SudoPath, fullArguments, cancellationToken, throwOnNonZero);
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

    public sealed record GamePortForwardingRule(int ExposedGamePort, string TargetHost, int TargetGamePort);

    private sealed record ProcessResult(int ExitCode, string Output);
}
