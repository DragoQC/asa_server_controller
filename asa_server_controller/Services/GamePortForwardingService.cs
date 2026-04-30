using System.Net;
using asa_server_controller.Data;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class GamePortForwardingService(
    IServiceScopeFactory serviceScopeFactory,
    RemoteServerInfoService remoteServerInfoService,
    ILogger<GamePortForwardingService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        remoteServerInfoService.InfoUpdated += OnRemoteServerInfoUpdated;

        try
        {
            await SynchronizeNowAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            remoteServerInfoService.InfoUpdated -= OnRemoteServerInfoUpdated;
        }
    }

    public Task SynchronizeNowAsync(CancellationToken cancellationToken = default)
    {
        return SynchronizeCoreAsync(cancellationToken);
    }

    private void OnRemoteServerInfoUpdated(int remoteServerId)
    {
        _ = SynchronizeInBackgroundAsync(remoteServerId);
    }

    private async Task SynchronizeInBackgroundAsync(int remoteServerId)
    {
        try
        {
            await SynchronizeNowAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to synchronize game port forwarding for remote server {RemoteServerId}.", remoteServerId);
        }
    }

    private async Task SynchronizeCoreAsync(CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SudoService sudoService = scope.ServiceProvider.GetRequiredService<SudoService>();

            List<PortForwardMapping> mappings = await dbContext.RemoteServers
                .Where(server =>
                    server.InviteStatus == "Accepted" &&
                    server.ExposedGamePort.HasValue &&
                    server.GamePort.HasValue)
                .OrderBy(server => server.Id)
                .Select(server => new PortForwardMapping(
                    server.Id,
                    string.IsNullOrWhiteSpace(server.ServerName) ? $"Server {server.Id}" : server.ServerName,
                    server.VpnAddress,
                    server.ExposedGamePort!.Value,
                    server.GamePort!.Value))
                .ToListAsync(cancellationToken);

            List<SudoService.GamePortForwardingRule> rules = [];
            HashSet<int> exposedPorts = [];

            foreach (PortForwardMapping mapping in mappings)
            {
                if (!TryNormalizeVpnHost(mapping.VpnAddress, out string vpnHost))
                {
                    logger.LogWarning("Skipping game port forward for server {RemoteServerId}. VPN address '{VpnAddress}' is not a valid IP address.", mapping.RemoteServerId, mapping.VpnAddress);
                    continue;
                }

                if (!exposedPorts.Add(mapping.ExposedGamePort))
                {
                    logger.LogWarning("Skipping duplicate exposed game port {ExposedGamePort}.", mapping.ExposedGamePort);
                    continue;
                }

                rules.Add(new SudoService.GamePortForwardingRule(mapping.ExposedGamePort, vpnHost, mapping.GamePort));
            }

            await sudoService.ApplyGamePortForwardingRulesAsync(rules, cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static bool TryNormalizeVpnHost(string vpnAddress, out string host)
    {
        string trimmed = vpnAddress.Trim();
        int slashIndex = trimmed.IndexOf('/');
        host = slashIndex >= 0 ? trimmed[..slashIndex] : trimmed;
        return IPAddress.TryParse(host, out _);
    }

    private sealed record PortForwardMapping(
        int RemoteServerId,
        string ServerName,
        string VpnAddress,
        int ExposedGamePort,
        int GamePort);
}
