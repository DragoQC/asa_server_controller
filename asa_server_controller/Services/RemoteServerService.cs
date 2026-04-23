using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Constants;
using asa_server_controller.Models.Servers;
using asa_server_controller.Models.Vpn;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace asa_server_controller.Services;

public sealed class RemoteServerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    RemoteServerHubClientService remoteServerHubClientService,
    VpnService vpnService,
    InvitationEventsService invitationEventsService,
    ModsEventsService modsEventsService)
{
    public const string DefaultRemoteServerPort = "8000";

    public async Task<IReadOnlyList<RemoteServerListItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<RemoteServerListItem> items = await dbContext.RemoteServers
            .Select(server => new RemoteServerListItem(
                server.Id,
                server.ServerName,
                server.VpnAddress,
                server.Port,
                server.ValidationStatus,
                false,
                false,
                false,
                false,
                server.MapName,
                0,
                server.MaxPlayers ?? 0,
                server.GamePort,
                server.ServerInfoCheckedAtUtc,
                server.LastSeenAtUtc,
                server.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<RemoteServerListItem> updatedItems = [];
        IReadOnlyDictionary<int, RemoteServerHubSnapshot> snapshots = remoteServerHubClientService.GetSnapshots();

        foreach (RemoteServerListItem item in items)
        {
            bool requiresExplicitPort = IsIpAddress(item.VpnAddress);
            if (requiresExplicitPort && !item.Port.HasValue)
            {
                updatedItems.Add(item with
                {
                    StateLabel = "Config needed",
                    IsOnline = false,
                    CanStart = false,
                    CanStop = false,
                    CanSendRconCommand = false,
                    MapName = string.Empty,
                    CurrentPlayers = 0,
                    MaxPlayers = 0,
                    LastSeenAtUtc = now
                });
                continue;
            }

            bool hasSnapshot = snapshots.TryGetValue(item.Id, out RemoteServerHubSnapshot? snapshot);
            if (hasSnapshot &&
                snapshot is not null &&
                string.Equals(snapshot.ConnectionState, "Connected", StringComparison.Ordinal))
            {
                if (!snapshot.AsaStatus.IsRunning)
                {
                    updatedItems.Add(item with
                    {
                        StateLabel = string.IsNullOrWhiteSpace(snapshot.AsaStatus.DisplayText) ? "Server offline" : snapshot.AsaStatus.DisplayText,
                        IsOnline = false,
                        CanStart = snapshot.AsaStatus.CanStart,
                        CanStop = snapshot.AsaStatus.CanStop,
                        CanSendRconCommand = snapshot.CanSendRconCommand,
                        MapName = string.Empty,
                        CurrentPlayers = snapshot.PlayerCount.CurrentPlayers,
                        MaxPlayers = item.MaxPlayers,
                        LastSeenAtUtc = now
                    });
                    continue;
                }

                updatedItems.Add(item with
                {
                    StateLabel = string.IsNullOrWhiteSpace(snapshot.AsaStatus.DisplayText) ? "Reachable" : snapshot.AsaStatus.DisplayText,
                    IsOnline = true,
                    CanStart = snapshot.AsaStatus.CanStart,
                    CanStop = snapshot.AsaStatus.CanStop,
                    CanSendRconCommand = snapshot.CanSendRconCommand,
                    MapName = item.MapName,
                    CurrentPlayers = snapshot.PlayerCount.CurrentPlayers,
                    MaxPlayers = item.MaxPlayers > 0 ? item.MaxPlayers : snapshot.PlayerCount.MaxPlayers,
                    LastSeenAtUtc = snapshot.UpdatedAtUtc
                });
                continue;
            }

            bool isReachable = item.Port.HasValue
                ? await IsTcpReachableAsync(GetIpAddress(item.VpnAddress), item.Port.Value)
                : false;
            bool isPingReachable = await IsPingReachableAsync(GetIpAddress(item.VpnAddress));

            if (isPingReachable)
            {
                updatedItems.Add(item with
                {
                    StateLabel = "Offline",
                    IsOnline = false,
                    CanStart = false,
                    CanStop = false,
                    CanSendRconCommand = false,
                    MapName = string.Empty,
                    CurrentPlayers = 0,
                    MaxPlayers = item.MaxPlayers,
                    LastSeenAtUtc = now
                });
                continue;
            }

            if (!isReachable)
            {
                updatedItems.Add(item with
                {
                    StateLabel = "Unknown",
                    IsOnline = false,
                    CanStart = false,
                    CanStop = false,
                    CanSendRconCommand = false,
                    MapName = string.Empty,
                    CurrentPlayers = 0,
                    MaxPlayers = 0
                });
                continue;
            }

            updatedItems.Add(item with
            {
                StateLabel = "Misconfigured",
                IsOnline = false,
                CanStart = false,
                CanStop = false,
                CanSendRconCommand = false,
                MapName = string.Empty,
                CurrentPlayers = 0,
                MaxPlayers = item.MaxPlayers,
                LastSeenAtUtc = now
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

    public async Task DeleteAsync(int remoteServerId, CancellationToken cancellationToken = default)
    {
        if (remoteServerId <= 0)
        {
            throw new InvalidOperationException("Server is required.");
        }

        List<(string InvitationKey, int InvitationId)> vpnInvitationFilesToDelete = [];

        await using (AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            RemoteServerEntity remoteServer = await dbContext.RemoteServers
                .Include(server => server.Invitations)
                .FirstOrDefaultAsync(server => server.Id == remoteServerId, cancellationToken)
                ?? throw new InvalidOperationException("Server was not found.");

            vpnInvitationFilesToDelete = remoteServer.Invitations
                .Select(invitation => (invitation.OneTimeVpnKey, invitation.Id))
                .ToList();

            List<NfsShareInviteEntity> nfsInvites = await dbContext.NfsShareInvites
                .Where(invite => invite.RemoteServerId == remoteServerId)
                .ToListAsync(cancellationToken);

            List<RemoteServerModEntity> modLinks = await dbContext.RemoteServerMods
                .Where(link => link.RemoteServerId == remoteServerId)
                .ToListAsync(cancellationToken);

            dbContext.NfsShareInvites.RemoveRange(nfsInvites);
            dbContext.RemoteServerMods.RemoveRange(modLinks);
            dbContext.Invitations.RemoveRange(remoteServer.Invitations);
            dbContext.RemoteServers.Remove(remoteServer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach ((string invitationKey, int invitationId) in vpnInvitationFilesToDelete)
        {
            await vpnService.DeleteInvitationFilesAsync(invitationKey, invitationId, cancellationToken);
        }

        await RebuildServerConfigIfReadyAsync(cancellationToken);
        await RestartWireGuardIfActiveAsync(cancellationToken);
        invitationEventsService.NotifyChanged();
        modsEventsService.NotifyChanged();
    }

    private async Task RebuildServerConfigIfReadyAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(VpnConstants.VpnConfigFilePath))
        {
            return;
        }

        VpnConfigModel vpnConfig = await vpnService.LoadConfiguredModelAsync(cancellationToken);
        SavedVpnKeyPair serverKeys = await vpnService.LoadServerKeyPairAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(serverKeys.PrivateKey))
        {
            return;
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<InvitationEntity> invitations = await dbContext.Invitations
            .Include(item => item.RemoteServer)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        List<(string PublicKey, string AllowedIp, string? PresharedKey)> peers = [];
        foreach (InvitationEntity invitation in invitations)
        {
            string? clientPublicKey = await vpnService.LoadInvitationClientPublicKeyAsync(invitation.OneTimeVpnKey, invitation.Id, cancellationToken);
            if (string.IsNullOrWhiteSpace(clientPublicKey))
            {
                continue;
            }

            peers.Add((
                clientPublicKey,
                invitation.RemoteServer.VpnAddress,
                string.IsNullOrWhiteSpace(vpnConfig.PresharedKey) ? null : vpnConfig.PresharedKey.Trim()));
        }

        string content = await vpnService.BuildServerConfigWithPeersAsync(vpnConfig, serverKeys.PrivateKey, peers, cancellationToken);
        await vpnService.SaveAsync(VpnConstants.VpnConfigFilePath, content, cancellationToken);
    }

    private async Task RestartWireGuardIfActiveAsync(CancellationToken cancellationToken)
    {
        if (!await vpnService.IsVpnActiveAsync(cancellationToken))
        {
            return;
        }

        await vpnService.RestartVpnAsync(cancellationToken);
    }

    private static string GetIpAddress(string address)
    {
        return address.Split('/', 2, StringSplitOptions.TrimEntries)[0];
    }

    private static bool IsIpAddress(string address)
    {
        return System.Net.IPAddress.TryParse(GetIpAddress(address), out _);
    }

    private static async Task<bool> IsTcpReachableAsync(string host, int port)
    {
        try
        {
            using TcpClient client = new();
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, cancellationTokenSource.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsPingReachableAsync(string host)
    {
        try
        {
            using Ping ping = new();
            PingReply reply = await ping.SendPingAsync(host, 1500);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}
