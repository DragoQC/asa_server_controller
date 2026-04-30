using System.Collections.Concurrent;
using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Servers;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class RemoteServerInfoService(
    IServiceScopeFactory serviceScopeFactory,
    RemoteServerHubClientService remoteServerHubClientService,
    RemoteServerAdminHubClientService remoteServerAdminHubClientService,
    ILogger<RemoteServerInfoService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<int, string> _lastActiveStateByServerId = new();

    public event Action<int>? InfoUpdated;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        remoteServerHubClientService.Changed += OnServerChanged;
        remoteServerAdminHubClientService.ServerInfoUpdated += OnServerInfoUpdated;

        return WaitForStopAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        remoteServerHubClientService.Changed -= OnServerChanged;
        remoteServerAdminHubClientService.ServerInfoUpdated -= OnServerInfoUpdated;
        return base.StopAsync(cancellationToken);
    }

    private void OnServerChanged(int remoteServerId)
    {
        RemoteAsaServiceStatus status = remoteServerHubClientService.GetSnapshot(remoteServerId).AsaStatus;
        _lastActiveStateByServerId.TryGetValue(remoteServerId, out string? previousActiveState);
        _lastActiveStateByServerId[remoteServerId] = status.ActiveState;

        if (!status.IsRunning || string.Equals(previousActiveState, status.ActiveState, StringComparison.Ordinal))
        {
            return;
        }

        _ = RefreshInBackgroundAsync(remoteServerId);
    }

    private void OnServerInfoUpdated(int remoteServerId)
    {
        _ = RefreshInBackgroundAsync(remoteServerId);
    }

    private async Task RefreshInBackgroundAsync(int remoteServerId)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            RemoteServerService remoteServerService = scope.ServiceProvider.GetRequiredService<RemoteServerService>();
            RemoteAdminHttpClient remoteAdminHttpClient = scope.ServiceProvider.GetRequiredService<RemoteAdminHttpClient>();
            RemoteServerModsService remoteServerModsService = scope.ServiceProvider.GetRequiredService<RemoteServerModsService>();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            RemoteServerConnection? connection = await remoteServerService.LoadConnectionAsync(remoteServerId);
            if (connection is null)
            {
                return;
            }

            RemoteServerInfoResponse? response = await remoteAdminHttpClient.GetFromJsonAsync<RemoteServerInfoResponse>(
                connection.BaseUrl,
                "/api/admin/server",
                connection.ApiKey);

            if (response is null || !response.Success)
            {
                return;
            }

            RemoteServerEntity? remoteServer = await dbContext.RemoteServers
                .FirstOrDefaultAsync(server => server.Id == remoteServerId);

            if (remoteServer is null)
            {
                return;
            }

            remoteServer.ServerName = response.ServerName?.Trim() ?? string.Empty;
            remoteServer.MapName = response.MapName?.Trim() ?? string.Empty;
            remoteServer.MaxPlayers = response.MaxPlayers;
            remoteServer.GamePort = response.GamePort;
            remoteServer.ServerInfoCheckedAtUtc = response.CheckedAtUtc;

            await dbContext.SaveChangesAsync();
            await remoteServerModsService.SyncRemoteServerAsync(remoteServerId, response.ModIds, CancellationToken.None);
            NotifyInfoUpdated(remoteServerId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to refresh server info for remote server {RemoteServerId}.", remoteServerId);
        }
    }

    private void NotifyInfoUpdated(int remoteServerId)
    {
        Action<int>? handlers = InfoUpdated;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<int> handler in handlers.GetInvocationList().Cast<Action<int>>())
        {
            try
            {
                handler(remoteServerId);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Remote server info subscriber failed for server {RemoteServerId}.", remoteServerId);
            }
        }
    }

    private static async Task WaitForStopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
