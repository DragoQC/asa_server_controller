using asa_server_controller.Models.Ini;
using asa_server_controller.Models.Servers;

namespace asa_server_controller.Services;

public sealed class RemoteIniFilesService(
    RemoteAdminHttpClient remoteAdminHttpClient,
    RemoteServerService remoteServerService)
{
    private const string ServerConfigBasePath = "api/admin/server-config";

    public Task<RemoteApiResult<RemoteIniFileResponse>> LoadGameIniAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        return LoadIniAsync(remoteServerId, ServerConfigBasePath + "/game-ini", cancellationToken);
    }

    public Task<RemoteApiResult<RemoteIniFileResponse>> LoadGameUserSettingsIniAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        return LoadIniAsync(remoteServerId, ServerConfigBasePath + "/game-user-settings-ini", cancellationToken);
    }

    public Task<RemoteApiResult<RemoteIniFileSaveResponse>> SaveGameIniAsync(int remoteServerId, string content, CancellationToken cancellationToken = default)
    {
        return SaveIniAsync(remoteServerId, ServerConfigBasePath + "/game-ini", "Game.ini", content, cancellationToken);
    }

    public Task<RemoteApiResult<RemoteIniFileSaveResponse>> SaveGameUserSettingsIniAsync(int remoteServerId, string content, CancellationToken cancellationToken = default)
    {
        return SaveIniAsync(remoteServerId, ServerConfigBasePath + "/game-user-settings-ini", "GameUserSettings.ini", content, cancellationToken);
    }

    private async Task<RemoteApiResult<RemoteIniFileResponse>> LoadIniAsync(int remoteServerId, string relativePath, CancellationToken cancellationToken)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        return await remoteAdminHttpClient.GetResultAsync<RemoteIniFileResponse>(
            connection.BaseUrl,
            relativePath,
            connection.ApiKey,
            cancellationToken);
    }

    private async Task<RemoteApiResult<RemoteIniFileSaveResponse>> SaveIniAsync(
        int remoteServerId,
        string relativePath,
        string fileName,
        string content,
        CancellationToken cancellationToken)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        return await remoteAdminHttpClient.PostFileAsync<RemoteIniFileSaveResponse>(
            connection.BaseUrl,
            relativePath,
            connection.ApiKey,
            fileName,
            content,
            cancellationToken);
    }
}
