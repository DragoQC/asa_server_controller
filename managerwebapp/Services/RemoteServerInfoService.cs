using System.Collections.Concurrent;
using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Servers;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Services;

public sealed class RemoteServerInfoService(
    IServiceScopeFactory serviceScopeFactory,
    RemoteServerHubClientService remoteServerHubClientService,
    ILogger<RemoteServerInfoService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<int, string> _lastActiveStateByServerId = new();

    public event Func<int, Task>? InfoUpdated;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        remoteServerHubClientService.StatusUpdated += OnStatusUpdatedAsync;

        return WaitForStopAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        remoteServerHubClientService.StatusUpdated -= OnStatusUpdatedAsync;
        return base.StopAsync(cancellationToken);
    }

    private Task OnStatusUpdatedAsync(int remoteServerId, RemoteAsaServiceStatus status)
    {
        _lastActiveStateByServerId.TryGetValue(remoteServerId, out string? previousActiveState);
        _lastActiveStateByServerId[remoteServerId] = status.ActiveState;

        if (!status.IsRunning || string.Equals(previousActiveState, status.ActiveState, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        _ = RefreshInBackgroundAsync(remoteServerId);
        return Task.CompletedTask;
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
                "/api/server/me",
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
            await NotifyInfoUpdatedAsync(remoteServerId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to refresh server info for remote server {RemoteServerId}.", remoteServerId);
        }
    }

    private async Task NotifyInfoUpdatedAsync(int remoteServerId)
    {
        Func<int, Task>? handlers = InfoUpdated;
        if (handlers is null)
        {
            return;
        }

        foreach (Func<int, Task> handler in handlers.GetInvocationList().Cast<Func<int, Task>>())
        {
            try
            {
                await handler(remoteServerId);
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
