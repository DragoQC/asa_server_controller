using asa_server_controller.Constants;
using asa_server_controller.Models.Servers;

namespace asa_server_controller.Services;

public sealed class RemoteServerConfigService(
    RemoteAdminHttpClient remoteAdminHttpClient,
    RemoteServerService remoteServerService)
{
    public Task<RemoteApiResult<RemoteServerConfigModel>> LoadAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        return SendGetAsync(remoteServerId, cancellationToken);
    }

    public Task<RemoteApiResult<RemoteServerConfigModel>> SaveAsync(
        int remoteServerId,
        PatchRemoteServerConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendPatchAsync(remoteServerId, request, cancellationToken);
    }

    private async Task<RemoteApiResult<RemoteServerConfigModel>> SendGetAsync(int remoteServerId, CancellationToken cancellationToken)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        return await remoteAdminHttpClient.GetResultAsync<RemoteServerConfigModel>(
            connection.BaseUrl,
            ClusterConstants.RemoteServerConfigPath,
            connection.ApiKey,
            cancellationToken);
    }

    private async Task<RemoteApiResult<RemoteServerConfigModel>> SendPatchAsync(
        int remoteServerId,
        PatchRemoteServerConfigRequest request,
        CancellationToken cancellationToken)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        return await remoteAdminHttpClient.PatchResultAsJsonAsync<PatchRemoteServerConfigRequest, RemoteServerConfigModel>(
            connection.BaseUrl,
            ClusterConstants.RemoteServerConfigPath,
            connection.ApiKey,
            request,
            cancellationToken);
    }
}
