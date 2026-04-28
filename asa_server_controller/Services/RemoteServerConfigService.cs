using asa_server_controller.Models.Servers;

namespace asa_server_controller.Services;

public sealed class RemoteServerConfigService(
    RemoteAdminHttpClient remoteAdminHttpClient,
    RemoteServerService remoteServerService)
{
    private const string ServerConfigPath = "api/admin/server-config";

    public async Task<RemoteServerConfigSettings> LoadAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        RemoteApiResult<RemoteServerConfigSettings> result = await remoteAdminHttpClient.GetResultAsync<RemoteServerConfigSettings>(
            connection.BaseUrl,
            ServerConfigPath,
            connection.ApiKey,
            cancellationToken);

        if (!result.Success || result.Data is null)
        {
            throw new InvalidOperationException(result.Message ?? "Failed to load remote server config.");
        }

        return result.Data;
    }

    public async Task SaveAsync(
        int remoteServerId,
        string serverName,
        string mapName,
        int maxPlayers,
        int gamePort,
        string modIds,
        string clusterId,
        CancellationToken cancellationToken = default)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);

        await remoteAdminHttpClient.PatchAsJsonAsync<PatchServerConfigRequest, object>(
            connection.BaseUrl,
            ServerConfigPath,
            connection.ApiKey,
            new PatchServerConfigRequest(
                serverName,
                mapName,
                maxPlayers,
                gamePort,
                NormalizeModIds(modIds),
                clusterId),
            cancellationToken);
    }

    private static IReadOnlyList<string> NormalizeModIds(string? modIds)
    {
        return string.IsNullOrWhiteSpace(modIds)
            ? []
            : modIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
    }
}
