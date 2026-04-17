using managerwebapp.Constants;
using managerwebapp.Models.Vpn;
using System.Diagnostics;

namespace managerwebapp.Services;

public sealed class VpnConfigService
{
    public const string DefaultAddress = "10.10.10.2/32";
    public const string DefaultListenPort = "51820";
    public const string DefaultAllowedIps = "10.10.10.0/24";
    public const string DefaultPersistentKeepalive = "25";

    public async Task<VpnKeyPair> GenerateKeyPairAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(VpnConstants.WgPath))
        {
            throw new InvalidOperationException("WireGuard is not installed.");
        }

        string privateKey = await RunProcessAsync(VpnConstants.WgPath, ["genkey"], null, cancellationToken);
        string publicKey = await RunProcessAsync(VpnConstants.WgPath, ["pubkey"], privateKey + "\n", cancellationToken);

        return new VpnKeyPair(privateKey, publicKey);
    }

    public async Task<SavedVpnKeyPair> GenerateAndSaveClientKeyPairAsync(CancellationToken cancellationToken = default)
    {
        VpnKeyPair keyPair = await GenerateKeyPairAsync(cancellationToken);
        await SaveKeyPairAsync(VpnConstants.ClientPrivateKeyFilePath, VpnConstants.ClientPublicKeyFilePath, keyPair, cancellationToken);
        return new SavedVpnKeyPair("Client", VpnConstants.ClientPrivateKeyFilePath, VpnConstants.ClientPublicKeyFilePath, keyPair.PrivateKey, keyPair.PublicKey, true);
    }

    public async Task<SavedVpnKeyPair> GenerateAndSaveServerKeyPairAsync(CancellationToken cancellationToken = default)
    {
        VpnKeyPair keyPair = await GenerateKeyPairAsync(cancellationToken);
        await SaveKeyPairAsync(VpnConstants.ServerPrivateKeyFilePath, VpnConstants.ServerPublicKeyFilePath, keyPair, cancellationToken);
        return new SavedVpnKeyPair("Server", VpnConstants.ServerPrivateKeyFilePath, VpnConstants.ServerPublicKeyFilePath, keyPair.PrivateKey, keyPair.PublicKey, true);
    }

    public async Task<SavedVpnKeyPair> LoadClientKeyPairAsync(CancellationToken cancellationToken = default)
    {
        return await LoadSavedKeyPairAsync("Client", VpnConstants.ClientPrivateKeyFilePath, VpnConstants.ClientPublicKeyFilePath, cancellationToken);
    }

    public async Task<SavedVpnKeyPair> LoadServerKeyPairAsync(CancellationToken cancellationToken = default)
    {
        return await LoadSavedKeyPairAsync("Server", VpnConstants.ServerPrivateKeyFilePath, VpnConstants.ServerPublicKeyFilePath, cancellationToken);
    }

    public async Task<VpnConfigModel> LoadModelAsync(CancellationToken cancellationToken = default)
    {
        string content = await LoadEditorContentAsync(VpnConstants.VpnConfigFilePath, cancellationToken);
        return ApplyDefaults(ParseModel(content));
    }

    public Task<string> BuildContentAsync(VpnConfigModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildContent(model));
    }

    public Task<bool> IsWireGuardInstalledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(VpnConstants.WgPath) && File.Exists(VpnConstants.WgQuickPath));
    }

    public Task<VpnConfigFileState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool exists = File.Exists(VpnConstants.VpnConfigFilePath);
        string stateLabel = exists ? "OK" : "Missing";

        return Task.FromResult(new VpnConfigFileState(
            "wg0.conf",
            "Main WireGuard configuration for the manager control node.",
            VpnConstants.VpnConfigFilePath,
            stateLabel,
            exists));
    }

    public async Task<string> LoadEditorContentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public async Task SaveAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(filePath, NormalizeContent(content), cancellationToken);
    }

    public VpnConfigModel CreateDefaultModel()
    {
        return ApplyDefaults(new VpnConfigModel());
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static async Task SaveKeyPairAsync(string privateKeyPath, string publicKeyPath, VpnKeyPair keyPair, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(VpnConstants.VpnDirectoryPath);
        await File.WriteAllTextAsync(privateKeyPath, NormalizeContent(keyPair.PrivateKey + "\n"), cancellationToken);
        await File.WriteAllTextAsync(publicKeyPath, NormalizeContent(keyPair.PublicKey + "\n"), cancellationToken);
    }

    private static async Task<SavedVpnKeyPair> LoadSavedKeyPairAsync(
        string name,
        string privateKeyPath,
        string publicKeyPath,
        CancellationToken cancellationToken)
    {
        bool exists = File.Exists(privateKeyPath) && File.Exists(publicKeyPath);
        if (!exists)
        {
            return new SavedVpnKeyPair(name, privateKeyPath, publicKeyPath, null, null, false);
        }

        string privateKey = (await File.ReadAllTextAsync(privateKeyPath, cancellationToken)).Trim();
        string publicKey = (await File.ReadAllTextAsync(publicKeyPath, cancellationToken)).Trim();
        return new SavedVpnKeyPair(name, privateKeyPath, publicKeyPath, privateKey, publicKey, true);
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardInput = true,
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

        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
        }

        process.StandardInput.Close();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        string stdout = (await stdoutTask).Trim();
        string stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "WireGuard command failed." : stderr);
        }

        return stdout;
    }

    private static VpnConfigModel ParseModel(string content)
    {
        VpnConfigModel model = new();
        string? currentSection = null;

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1];
                continue;
            }

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();

            if (string.Equals(currentSection, "Interface", StringComparison.OrdinalIgnoreCase))
            {
                switch (key)
                {
                    case "PrivateKey":
                        model.PrivateKey = value;
                        break;
                    case "Address":
                        model.Address = value;
                        break;
                    case "ListenPort":
                        model.ListenPort = value;
                        break;
                    case "DNS":
                        model.Dns = value;
                        break;
                }
            }
            else if (string.Equals(currentSection, "Peer", StringComparison.OrdinalIgnoreCase))
            {
                switch (key)
                {
                    case "PublicKey":
                        model.PeerPublicKey = value;
                        break;
                    case "PresharedKey":
                        model.PresharedKey = value;
                        break;
                    case "Endpoint":
                        model.Endpoint = value;
                        break;
                    case "AllowedIPs":
                        model.AllowedIps = value;
                        break;
                    case "PersistentKeepalive":
                        model.PersistentKeepalive = value;
                        break;
                }
            }
        }

        return model;
    }

    private static string BuildContent(VpnConfigModel model)
    {
        List<string> lines =
        [
            "[Interface]"
        ];

        AppendLine(lines, "PrivateKey", model.PrivateKey);
        AppendLine(lines, "Address", model.Address);
        AppendLine(lines, "ListenPort", model.ListenPort);
        AppendLine(lines, "DNS", model.Dns);

        lines.Add(string.Empty);
        lines.Add("[Peer]");
        AppendLine(lines, "PublicKey", model.PeerPublicKey);
        AppendLine(lines, "PresharedKey", model.PresharedKey);
        AppendLine(lines, "Endpoint", model.Endpoint);
        AppendLine(lines, "AllowedIPs", model.AllowedIps);
        AppendLine(lines, "PersistentKeepalive", model.PersistentKeepalive);

        return NormalizeContent(string.Join('\n', lines).TrimEnd() + "\n");
    }

    private static void AppendLine(ICollection<string> lines, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{key} = {value.Trim()}");
        }
    }

    private static VpnConfigModel ApplyDefaults(VpnConfigModel model)
    {
        model.Address = string.IsNullOrWhiteSpace(model.Address) ? DefaultAddress : model.Address;
        model.ListenPort = string.IsNullOrWhiteSpace(model.ListenPort) ? DefaultListenPort : model.ListenPort;
        model.AllowedIps = string.IsNullOrWhiteSpace(model.AllowedIps) ? DefaultAllowedIps : model.AllowedIps;
        model.PersistentKeepalive = string.IsNullOrWhiteSpace(model.PersistentKeepalive) ? DefaultPersistentKeepalive : model.PersistentKeepalive;
        return model;
    }
}
