using asa_server_controller.Constants;
using asa_server_controller.Models.Servers;

namespace asa_server_controller.Services;

public sealed class RemoteRconService(
    RemoteAdminHttpClient remoteAdminHttpClient,
    RemoteServerService remoteServerService)
{
    public async Task<string> GetEndpointUrlAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        return $"{connection.BaseUrl.TrimEnd('/')}/{RemoteRconConstants.Path}";
    }

    public async Task<RemoteRconCommandResponse> ExecuteAsync(int remoteServerId, string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("RCON command is required.");
        }

        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        RemoteRconCommandResponse? response = await remoteAdminHttpClient.PostAsJsonAsync<RemoteRconCommandRequest, RemoteRconCommandResponse>(
            connection.BaseUrl,
            RemoteRconConstants.Path,
            connection.ApiKey,
            new RemoteRconCommandRequest(command.Trim()),
            cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException("Remote server returned an empty RCON response.");
        }

        return response;
    }
}
