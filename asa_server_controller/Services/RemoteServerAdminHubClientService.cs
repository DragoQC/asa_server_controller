using System.Collections.Concurrent;
using asa_server_controller.Constants;
using asa_server_controller.Models.Servers;
using Microsoft.AspNetCore.SignalR.Client;

namespace asa_server_controller.Services;

public sealed class RemoteServerAdminHubClientService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoteServerAdminHubClientService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<int, RemoteHubRegistration> _connections = new();
    private readonly ConcurrentDictionary<int, RemoteAdminHostMetricsSnapshot> _snapshots = new();
    private readonly SemaphoreSlim _synchronizeLock = new(1, 1);
    public event Action<int>? Changed;
    public event Action<int>? ServerInfoUpdated;

    public IReadOnlyDictionary<int, RemoteAdminHostMetricsSnapshot> GetSnapshots()
    {
        return new Dictionary<int, RemoteAdminHostMetricsSnapshot>(_snapshots);
    }

    public RemoteAdminHostMetricsSnapshot GetSnapshot(int remoteServerId)
    {
        return _snapshots.TryGetValue(remoteServerId, out RemoteAdminHostMetricsSnapshot? snapshot)
            ? snapshot
            : RemoteAdminHostMetricsSnapshot.Default(remoteServerId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SynchronizeNowAsync(stoppingToken);

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(20));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SynchronizeNowAsync(stoppingToken);
        }
    }

    public async Task SynchronizeNowAsync(CancellationToken cancellationToken = default)
    {
        await _synchronizeLock.WaitAsync(cancellationToken);

        try
        {
            await SynchronizeAsync(cancellationToken);
        }
        finally
        {
            _synchronizeLock.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach ((_, RemoteHubRegistration registration) in _connections)
        {
            await DisposeConnectionAsync(registration.Connection);
        }

        _connections.Clear();
        await base.StopAsync(cancellationToken);
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        RemoteServerService remoteServerService = scope.ServiceProvider.GetRequiredService<RemoteServerService>();
        IReadOnlyList<RemoteServerConnection> servers = await remoteServerService.LoadConnectionsAsync(cancellationToken);
        HashSet<int> knownIds = servers.Select(server => server.Id).ToHashSet();

        foreach (RemoteServerConnection server in servers)
        {
            RemoteHubRegistration registration = _connections.GetOrAdd(server.Id, _ =>
            {
                HubConnection connection = BuildConnection(server);
                return new RemoteHubRegistration(connection, server.BaseUrl, server.ApiKey);
            });

            if (!string.Equals(registration.BaseUrl, server.BaseUrl, StringComparison.Ordinal) ||
                !string.Equals(registration.ApiKey, server.ApiKey, StringComparison.Ordinal))
            {
                await ReplaceConnectionAsync(server, registration);
                continue;
            }

            await EnsureStartedAsync(server.Id, registration.Connection, cancellationToken);
        }

        int[] staleIds = _connections.Keys.Where(id => !knownIds.Contains(id)).ToArray();
        foreach (int staleId in staleIds)
        {
            if (_connections.TryRemove(staleId, out RemoteHubRegistration? registration))
            {
                await DisposeConnectionAsync(registration.Connection);
                _snapshots.TryRemove(staleId, out _);
            }
        }
    }

    private async Task ReplaceConnectionAsync(RemoteServerConnection server, RemoteHubRegistration registration)
    {
        await DisposeConnectionAsync(registration.Connection);
        HubConnection connection = BuildConnection(server);
        _connections[server.Id] = new RemoteHubRegistration(connection, server.BaseUrl, server.ApiKey);
        await EnsureStartedAsync(server.Id, connection, CancellationToken.None);
    }

    private HubConnection BuildConnection(RemoteServerConnection server)
    {
        Uri hubUri = new($"{server.BaseUrl.Trim().TrimEnd('/')}{AdminStateHubConstants.Route}", UriKind.Absolute);
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Headers["X-Api-Key"] = server.ApiKey;
            })
            .WithAutomaticReconnect()
            .Build();

        _snapshots[server.Id] = RemoteAdminHostMetricsSnapshot.Default(server.Id);

        connection.On<RemoteAdminHostMetricsSnapshot>(AdminStateHubConstants.HostMetricsUpdatedMethod, snapshot =>
        {
            _snapshots[server.Id] = snapshot with
            {
                RemoteServerId = server.Id,
                ConnectionState = connection.State.ToString(),
            };

            NotifyChanged(server.Id);
        });

        connection.On(AdminStateHubConstants.ServerInfoUpdatedMethod, () =>
        {
            NotifyServerInfoUpdated(server.Id);
        });

        connection.Reconnecting += error =>
        {
            logger.LogWarning(error, "Remote admin hub reconnecting for server {RemoteServerId}.", server.Id);
            UpdateConnectionState(server.Id, "Reconnecting");
            return Task.CompletedTask;
        };

        connection.Reconnected += _ =>
        {
            UpdateConnectionState(server.Id, connection.State.ToString());
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            if (error is not null)
            {
                logger.LogWarning(error, "Remote admin hub closed for server {RemoteServerId}.", server.Id);
            }

            UpdateConnectionState(server.Id, "Disconnected");
            return Task.CompletedTask;
        };

        return connection;
    }

    private async Task EnsureStartedAsync(int remoteServerId, HubConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != HubConnectionState.Disconnected)
        {
            return;
        }

        try
        {
            await connection.StartAsync(cancellationToken);
            UpdateConnectionState(remoteServerId, connection.State.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to connect to remote admin hub for server {RemoteServerId}.", remoteServerId);
            UpdateConnectionState(remoteServerId, "Disconnected");
        }
    }

    private void UpdateConnectionState(int remoteServerId, string connectionState)
    {
        _snapshots.AddOrUpdate(
            remoteServerId,
            _ => RemoteAdminHostMetricsSnapshot.Default(remoteServerId) with
            {
                ConnectionState = connectionState,
            },
            (_, current) => current with
            {
                ConnectionState = connectionState,
            });

        NotifyChanged(remoteServerId);
    }

    private void NotifyChanged(int remoteServerId)
    {
        Action<int>? handlers = Changed;
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
                logger.LogWarning(exception, "Remote admin hub subscriber failed for server {RemoteServerId}.", remoteServerId);
            }
        }
    }

    private void NotifyServerInfoUpdated(int remoteServerId)
    {
        Action<int>? handlers = ServerInfoUpdated;
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
                logger.LogWarning(exception, "Remote admin server info subscriber failed for server {RemoteServerId}.", remoteServerId);
            }
        }
    }

    private static async Task DisposeConnectionAsync(HubConnection connection)
    {
        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
        }
    }
}
