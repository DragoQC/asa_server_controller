using System.Collections.Concurrent;
using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Servers;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Services;

public sealed class RemoteServerModsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    RemoteAdminHttpClient remoteAdminHttpClient,
    RemoteServerService remoteServerService,
    RemoteServerHubClientService remoteServerHubClientService,
    ModsService modsService)
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _syncLocks = new();

    public async Task SyncAcceptedServersAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        int[] serverIds = await dbContext.RemoteServers
            .Where(server => server.InviteStatus == "Accepted" && !string.IsNullOrWhiteSpace(server.ApiKey))
            .Select(server => server.Id)
            .ToArrayAsync(cancellationToken);

        foreach (int serverId in serverIds)
        {
            await SyncRemoteServerAsync(serverId, cancellationToken);
        }
    }

    public async Task SyncRemoteServerAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        RemoteServerConnection connection = await remoteServerService.LoadRequiredConnectionAsync(remoteServerId, cancellationToken);
        RemoteServerInfoResponse? response = await remoteAdminHttpClient.GetFromJsonAsync<RemoteServerInfoResponse>(
            connection.BaseUrl,
            "/api/server/me",
            connection.ApiKey,
            cancellationToken);

        if (response is null || !response.Success)
        {
            throw new InvalidOperationException($"Remote server '{connection.BaseUrl}' did not return a valid server info payload.");
        }

        await SyncRemoteServerAsync(remoteServerId, response.ModIds, cancellationToken);
    }

    public async Task SyncRemoteServerAsync(int remoteServerId, IEnumerable<string>? remoteModIds, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim syncLock = _syncLocks.GetOrAdd(remoteServerId, _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken);

        try
        {
            long[] modIds = (remoteModIds ?? [])
                .Select(value => long.TryParse(value, out long parsed) ? parsed : 0)
                .Where(value => value > 0)
                .Distinct()
                .ToArray();

            await modsService.EnsureModsCachedAsync(modIds, cancellationToken);

            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            RemoteServerEntity remoteServer = await dbContext.RemoteServers
                .FirstAsync(server => server.Id == remoteServerId, cancellationToken);

            List<RemoteServerModEntity> existingLinks = await dbContext.RemoteServerMods
                .Where(link => link.RemoteServerId == remoteServerId)
                .ToListAsync(cancellationToken);

            Dictionary<int, long> existingByLinkId = await dbContext.RemoteServerMods
                .Where(link => link.RemoteServerId == remoteServerId)
                .Join(
                    dbContext.Mods,
                    link => link.ModEntityId,
                    mod => mod.Id,
                    (link, mod) => new { link.Id, mod.CurseForgeModId })
                .ToDictionaryAsync(item => item.Id, item => item.CurseForgeModId, cancellationToken);

            int[] linkIdsToRemove = existingByLinkId
                .Where(item => !modIds.Contains(item.Value))
                .Select(item => item.Key)
                .ToArray();

            if (linkIdsToRemove.Length > 0)
            {
                List<RemoteServerModEntity> linksToRemove = existingLinks
                    .Where(link => linkIdsToRemove.Contains(link.Id))
                    .ToList();

                dbContext.RemoteServerMods.RemoveRange(linksToRemove);
            }

            Dictionary<long, int> modEntityIdsByCurseForgeId = await dbContext.Mods
                .Where(mod => modIds.Contains(mod.CurseForgeModId))
                .ToDictionaryAsync(mod => mod.CurseForgeModId, mod => mod.Id, cancellationToken);

            HashSet<int> linkedModEntityIds = existingLinks
                .Select(link => link.ModEntityId)
                .ToHashSet();

            foreach (long modId in modIds)
            {
                if (!modEntityIdsByCurseForgeId.TryGetValue(modId, out int modEntityId) ||
                    linkedModEntityIds.Contains(modEntityId))
                {
                    continue;
                }

                dbContext.RemoteServerMods.Add(new RemoteServerModEntity
                {
                    RemoteServerId = remoteServerId,
                    ModEntityId = modEntityId
                });
            }

            remoteServer.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            syncLock.Release();
        }
    }

    public async Task<IReadOnlyList<PublicServerOverviewItem>> LoadOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<RemoteServerEntity> servers = await dbContext.RemoteServers
            .Where(server => server.InviteStatus == "Accepted")
            .OrderBy(server => server.VpnAddress)
            .ToListAsync(cancellationToken);

        int[] serverIds = servers.Select(server => server.Id).ToArray();
        List<(int RemoteServerId, PublicServerModItem Mod)> mods = await dbContext.RemoteServerMods
            .Where(link => serverIds.Contains(link.RemoteServerId))
            .Join(
                dbContext.Mods,
                link => link.ModEntityId,
                mod => mod.Id,
                (link, mod) => new ValueTuple<int, PublicServerModItem>(
                    link.RemoteServerId,
                    new PublicServerModItem(
                        mod.CurseForgeModId,
                        mod.Name,
                        mod.Summary,
                        mod.WebsiteUrl,
                        mod.LogoUrl)))
            .ToListAsync(cancellationToken);

        Dictionary<int, IReadOnlyList<PublicServerModItem>> modsByServerId = mods
            .GroupBy(item => item.RemoteServerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicServerModItem>)group
                    .Select(item => item.Mod)
                    .OrderBy(item => item.Name)
                    .ToList());

        return servers
            .Select(server =>
            {
                RemoteServerHubSnapshot snapshot = remoteServerHubClientService.GetSnapshot(server.Id);

                return new PublicServerOverviewItem(
                    server.Id,
                    server.ServerName,
                    server.VpnAddress,
                    server.Port,
                    snapshot.ConnectionState,
                    server.ValidationStatus,
                    snapshot.PlayerCount.CurrentPlayers,
                    server.MaxPlayers ?? snapshot.PlayerCount.MaxPlayers,
                    server.MapName,
                    modsByServerId.TryGetValue(server.Id, out IReadOnlyList<PublicServerModItem>? items)
                        ? items
                        : []);
            })
            .ToList();
    }
}
