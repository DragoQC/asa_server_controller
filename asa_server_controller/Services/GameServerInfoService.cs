using asa_server_controller.Data;
using asa_server_controller.Models.Servers;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class GameServerInfoService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<GameServerPortsSummary> LoadPortsSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<GameServerPortInfo> servers = await dbContext.RemoteServers
            .OrderBy(server => server.Id)
            .Select(server => new GameServerPortInfo(
                server.Id,
                string.IsNullOrWhiteSpace(server.ServerName) ? $"Server {server.Id}" : server.ServerName,
                server.VpnAddress,
                server.ExposedGamePort,
                server.GamePort))
            .ToListAsync(cancellationToken);

        List<string> missingServerPorts = servers
            .Where(server => server.GamePort is null)
            .Select(server => server.Name)
            .ToList();

        return new GameServerPortsSummary(servers, missingServerPorts);
    }
}
