using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Servers;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;

namespace managerwebapp.Services;

public sealed class RemoteServerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    RemoteServerHubClientService remoteServerHubClientService)
{
    public const string DefaultRemoteServerPort = "8000";

    public async Task<IReadOnlyList<RemoteServerListItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<RemoteServerListItem> items = await dbContext.RemoteServers
            .Select(server => new RemoteServerListItem(
                server.Id,
                server.VpnAddress,
                server.Port,
                server.ValidationStatus,
                false,
                false,
                false,
                server.LastSeenAtUtc,
                server.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<RemoteServerListItem> updatedItems = [];
        IReadOnlyDictionary<int, RemoteServerHubSnapshot> snapshots = remoteServerHubClientService.GetSnapshots();

        foreach (RemoteServerListItem item in items)
        {
            bool isReachable = await PingAddressAsync(GetIpAddress(item.VpnAddress));
            if (!isReachable)
            {
                updatedItems.Add(item with
                {
                    StateLabel = "Unknown",
                    CanStart = false,
                    CanStop = false,
                    CanOpenRcon = false
                });
                continue;
            }

            if (!item.Port.HasValue)
            {
                updatedItems.Add(item with
                {
                    StateLabel = "Config needed",
                    CanStart = false,
                    CanStop = false,
                    CanOpenRcon = false,
                    LastSeenAtUtc = now
                });
                continue;
            }

            if (!snapshots.TryGetValue(item.Id, out RemoteServerHubSnapshot? snapshot) ||
                !string.Equals(snapshot.ConnectionState, "Connected", StringComparison.Ordinal))
            {
                updatedItems.Add(item with
                {
                    StateLabel = "Misconfigured",
                    CanStart = false,
                    CanStop = false,
                    CanOpenRcon = false,
                    LastSeenAtUtc = now
                });
                continue;
            }

            updatedItems.Add(item with
            {
                StateLabel = string.IsNullOrWhiteSpace(snapshot.AsaStatus.DisplayText) ? "Reachable" : snapshot.AsaStatus.DisplayText,
                CanStart = snapshot.AsaStatus.CanStart,
                CanStop = snapshot.AsaStatus.CanStop,
                CanOpenRcon = snapshot.AsaStatus.IsRunning,
                LastSeenAtUtc = snapshot.UpdatedAtUtc
            });
        }

        return updatedItems
            .OrderByDescending(server => server.CreatedAtUtc)
            .ToList();
    }

    public async Task<RemoteServerConnection?> LoadConnectionAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.RemoteServers
            .Where(server => server.Id == remoteServerId)
            .Select(server => new RemoteServerConnection(
                server.Id,
                server.VpnAddress,
                server.Port,
                server.ApiKey))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RemoteServerConnection> LoadRequiredConnectionAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        RemoteServerConnection? connection = await LoadConnectionAsync(remoteServerId, cancellationToken);
        return connection ?? throw new InvalidOperationException($"Remote server '{remoteServerId}' was not found.");
    }

    public async Task<IReadOnlyList<RemoteServerConnection>> LoadConnectionsAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.RemoteServers
            .Where(server => server.InviteStatus == "Accepted" && server.Port.HasValue && !string.IsNullOrWhiteSpace(server.ApiKey))
            .OrderBy(server => server.Id)
            .Select(server => new RemoteServerConnection(
                server.Id,
                server.VpnAddress,
                server.Port,
                server.ApiKey))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(int remoteServerId, string vpnAddress, string? port, CancellationToken cancellationToken = default)
    {
        if (remoteServerId <= 0)
        {
            throw new InvalidOperationException("Server is required.");
        }

        if (string.IsNullOrWhiteSpace(vpnAddress))
        {
            throw new InvalidOperationException("VPN address is required.");
        }

        int? parsedPort = null;
        if (!string.IsNullOrWhiteSpace(port))
        {
            string normalizedPort = port.Trim();
            if (!int.TryParse(normalizedPort, out int portValue) || portValue <= 0)
            {
                throw new InvalidOperationException("Port is invalid.");
            }

            parsedPort = portValue;
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string normalizedVpnAddress = vpnAddress.Trim();

        bool exists = await dbContext.RemoteServers.AnyAsync(
            server => server.Id != remoteServerId && server.VpnAddress == normalizedVpnAddress,
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Server is already registered.");
        }

        RemoteServerEntity remoteServer = await dbContext.RemoteServers
            .FirstOrDefaultAsync(server => server.Id == remoteServerId, cancellationToken)
            ?? throw new InvalidOperationException("Server was not found.");

        remoteServer.VpnAddress = normalizedVpnAddress;
        remoteServer.Port = parsedPort;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetIpAddress(string address)
    {
        return address.Split('/', 2, StringSplitOptions.TrimEntries)[0];
    }

    private static async Task<bool> PingAddressAsync(string ipAddress)
    {
        try
        {
            using Ping ping = new();
            PingReply reply = await ping.SendPingAsync(ipAddress, 1500);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}
