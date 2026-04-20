using managerwebapp.Constants;
using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Vpn;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace managerwebapp.Services;

public sealed class VpnService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const int SettingsId = 1;
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
        VpnConfigModel model = ParseServerModel(content);
        VpnServerSettingsModel settings = await LoadSettingsAsync(cancellationToken);
        return ApplyDefaults(MergeServerSettings(model, settings));
    }

    public async Task<VpnConfigModel> LoadConfiguredModelAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(VpnConstants.VpnConfigFilePath))
        {
            throw new InvalidOperationException("Configure and save wg0.conf before using invitations.");
        }

        string content = await LoadEditorContentAsync(VpnConstants.VpnConfigFilePath, cancellationToken);
        VpnConfigModel model = ParseServerModel(content);

        if (string.IsNullOrWhiteSpace(model.Address))
        {
            throw new InvalidOperationException("VPN address must be configured in wg0.conf before using invitations.");
        }

        if (string.IsNullOrWhiteSpace(model.ListenPort))
        {
            throw new InvalidOperationException("VPN listen port must be configured in wg0.conf before using invitations.");
        }

        VpnServerSettingsModel settings = await LoadSettingsAsync(cancellationToken);
        VpnConfigModel configuredModel = MergeServerSettings(model, settings);

        if (string.IsNullOrWhiteSpace(configuredModel.Endpoint))
        {
            throw new InvalidOperationException("VPN endpoint must be configured before using invitations.");
        }

        if (string.IsNullOrWhiteSpace(configuredModel.AllowedIps))
        {
            throw new InvalidOperationException("VPN allowed IPs must be configured before using invitations.");
        }

        return configuredModel;
    }

    public async Task<VpnServerSettingsModel> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        VpnServerSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);

        return new VpnServerSettingsModel
        {
            Endpoint = string.IsNullOrWhiteSpace(settings.Endpoint) ? null : settings.Endpoint,
            AllowedIps = string.IsNullOrWhiteSpace(settings.AllowedIps) ? DefaultAllowedIps : settings.AllowedIps,
            PersistentKeepalive = string.IsNullOrWhiteSpace(settings.PersistentKeepalive) ? DefaultPersistentKeepalive : settings.PersistentKeepalive,
            PresharedKey = string.IsNullOrWhiteSpace(settings.PresharedKey) ? null : settings.PresharedKey
        };
    }

    public async Task SaveSettingsAsync(VpnServerSettingsModel model, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        VpnServerSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);
        settings.Endpoint = model.Endpoint?.Trim() ?? string.Empty;
        settings.AllowedIps = model.AllowedIps?.Trim() ?? string.Empty;
        settings.PersistentKeepalive = model.PersistentKeepalive?.Trim() ?? string.Empty;
        settings.PresharedKey = model.PresharedKey?.Trim() ?? string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> LoadConfiguredAddressAsync(CancellationToken cancellationToken = default)
    {
        VpnConfigModel model = await LoadConfiguredModelAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(model.Address)
            ? throw new InvalidOperationException("VPN address must be configured in wg0.conf before using cluster configuration.")
            : model.Address.Trim();
    }

    public async Task<string> LoadConfiguredIpAddressAsync(CancellationToken cancellationToken = default)
    {
        string configuredAddress = await LoadConfiguredAddressAsync(cancellationToken);
        return configuredAddress.Split('/', 2, StringSplitOptions.TrimEntries)[0];
    }

    public async Task<string> LoadCurrentAddressAsync(CancellationToken cancellationToken = default)
    {
        VpnConfigModel model = await LoadModelAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(model.Address) ? DefaultAddress : model.Address.Trim();
    }

    public async Task<string> LoadCurrentIpAddressAsync(CancellationToken cancellationToken = default)
    {
        string currentAddress = await LoadCurrentAddressAsync(cancellationToken);
        return currentAddress.Split('/', 2, StringSplitOptions.TrimEntries)[0];
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadConfiguredModelAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public Task<string> BuildContentAsync(VpnConfigModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildServerContent(model));
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
        string normalizedContent = NormalizeContent(content);
        await File.WriteAllTextAsync(filePath, normalizedContent, cancellationToken);
    }

    public string GetInvitationDirectoryPath(int invitationId)
    {
        return Path.Combine(VpnConstants.InvitationDirectoryPath, invitationId.ToString());
    }

    public string GetInvitationConfigFilePath(int invitationId)
    {
        return Path.Combine(GetInvitationDirectoryPath(invitationId), "wg0.conf");
    }

    public string GetInvitationClientPrivateKeyFilePath(int invitationId)
    {
        return Path.Combine(GetInvitationDirectoryPath(invitationId), "client.key");
    }

    public string GetInvitationClientPublicKeyFilePath(int invitationId)
    {
        return Path.Combine(GetInvitationDirectoryPath(invitationId), "client.pub");
    }

    public async Task SaveInvitationFilesAsync(
        int invitationId,
        VpnConfigModel model,
        string vpnAddress,
        string clientPrivateKey,
        string clientPublicKey,
        string serverPublicKey,
        CancellationToken cancellationToken = default)
    {
        string invitationDirectoryPath = GetInvitationDirectoryPath(invitationId);
        Directory.CreateDirectory(invitationDirectoryPath);

        await File.WriteAllTextAsync(GetInvitationClientPrivateKeyFilePath(invitationId), NormalizeContent(clientPrivateKey.Trim() + "\n"), cancellationToken);
        await File.WriteAllTextAsync(GetInvitationClientPublicKeyFilePath(invitationId), NormalizeContent(clientPublicKey.Trim() + "\n"), cancellationToken);

        string invitationConfigContent = BuildInvitationConfigContent(model, vpnAddress, clientPrivateKey, serverPublicKey);
        await File.WriteAllTextAsync(GetInvitationConfigFilePath(invitationId), invitationConfigContent, cancellationToken);
    }

    public Task<string> LoadInvitationConfigContentAsync(int invitationId, CancellationToken cancellationToken = default)
    {
        return LoadEditorContentAsync(GetInvitationConfigFilePath(invitationId), cancellationToken);
    }

    public async Task<string?> LoadInvitationClientPublicKeyAsync(int invitationId, CancellationToken cancellationToken = default)
    {
        string filePath = GetInvitationClientPublicKeyFilePath(invitationId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return (await File.ReadAllTextAsync(filePath, cancellationToken)).Trim();
    }

    public Task<string> BuildServerConfigWithPeersAsync(
        VpnConfigModel model,
        string serverPrivateKey,
        IReadOnlyList<(string PublicKey, string AllowedIp, string? PresharedKey)> peers,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildServerConfigWithPeers(model, serverPrivateKey, peers));
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

    private static VpnConfigModel ParseServerModel(string content)
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
                }
            }
        }

        return model;
    }

    private static VpnConfigModel MergeServerSettings(VpnConfigModel serverModel, VpnServerSettingsModel settings)
    {
        serverModel.Endpoint = settings.Endpoint;
        serverModel.AllowedIps = settings.AllowedIps;
        serverModel.PersistentKeepalive = settings.PersistentKeepalive;
        serverModel.PresharedKey = settings.PresharedKey;
        return serverModel;
    }

    private static string BuildServerContent(VpnConfigModel model)
    {
        List<string> lines =
        [
            "[Interface]"
        ];

        AppendLine(lines, "PrivateKey", model.PrivateKey);
        AppendLine(lines, "Address", model.Address);
        AppendLine(lines, "ListenPort", model.ListenPort);

        return NormalizeContent(string.Join('\n', lines).TrimEnd() + "\n");
    }

    private static string BuildInvitationConfigContent(
        VpnConfigModel model,
        string vpnAddress,
        string clientPrivateKey,
        string serverPublicKey)
    {
        List<string> lines =
        [
            "[Interface]"
        ];

        AppendLine(lines, "PrivateKey", clientPrivateKey);
        AppendLine(lines, "Address", vpnAddress);

        lines.Add(string.Empty);
        lines.Add("[Peer]");
        AppendLine(lines, "PublicKey", serverPublicKey);
        AppendLine(lines, "PresharedKey", model.PresharedKey);
        AppendLine(lines, "Endpoint", BuildEndpointValue(model.Endpoint, model.ListenPort));
        AppendLine(lines, "AllowedIPs", model.AllowedIps);
        AppendLine(lines, "PersistentKeepalive", model.PersistentKeepalive);

        return NormalizeContent(string.Join('\n', lines).TrimEnd() + "\n");
    }

    private static string BuildServerConfigWithPeers(
        VpnConfigModel model,
        string serverPrivateKey,
        IReadOnlyList<(string PublicKey, string AllowedIp, string? PresharedKey)> peers)
    {
        List<string> lines =
        [
            "[Interface]"
        ];

        AppendLine(lines, "PrivateKey", serverPrivateKey);
        AppendLine(lines, "Address", model.Address);
        AppendLine(lines, "ListenPort", model.ListenPort);

        foreach ((string publicKey, string allowedIp, string? presharedKey) in peers)
        {
            lines.Add(string.Empty);
            lines.Add("[Peer]");
            AppendLine(lines, "PublicKey", publicKey);
            AppendLine(lines, "PresharedKey", presharedKey);
            AppendLine(lines, "AllowedIPs", allowedIp);
        }

        return NormalizeContent(string.Join('\n', lines).TrimEnd() + "\n");
    }

    private static void AppendLine(ICollection<string> lines, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{key} = {value.Trim()}");
        }
    }

    private static string? BuildEndpointValue(string? endpoint, string? listenPort)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        string trimmedEndpoint = endpoint.Trim();
        if (trimmedEndpoint.Contains(':', StringComparison.Ordinal))
        {
            return trimmedEndpoint;
        }

        return string.IsNullOrWhiteSpace(listenPort)
            ? trimmedEndpoint
            : $"{trimmedEndpoint}:{listenPort.Trim()}";
    }

    private static VpnConfigModel ApplyDefaults(VpnConfigModel model)
    {
        model.Address = string.IsNullOrWhiteSpace(model.Address) ? DefaultAddress : model.Address;
        model.ListenPort = string.IsNullOrWhiteSpace(model.ListenPort) ? DefaultListenPort : model.ListenPort;
        model.AllowedIps = string.IsNullOrWhiteSpace(model.AllowedIps) ? DefaultAllowedIps : model.AllowedIps;
        model.PersistentKeepalive = string.IsNullOrWhiteSpace(model.PersistentKeepalive) ? DefaultPersistentKeepalive : model.PersistentKeepalive;
        return model;
    }

    private static async Task<VpnServerSettingsEntity> GetOrCreateSettingsEntityAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        VpnServerSettingsEntity? settings = await dbContext.VpnServerSettings
            .FirstOrDefaultAsync(entity => entity.Id == SettingsId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new VpnServerSettingsEntity
        {
            Id = SettingsId
        };

        dbContext.VpnServerSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
