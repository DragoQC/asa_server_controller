using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Servers;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Services;

public sealed class RemoteServerService(IDbContextFactory<AppDbContext> dbContextFactory)
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
                server.InviteStatus,
                server.ValidationStatus,
                server.LastSeenAtUtc,
                server.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return items
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
            .OrderBy(server => server.Id)
            .Select(server => new RemoteServerConnection(
                server.Id,
                server.VpnAddress,
                server.Port,
                server.ApiKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimedInvitationOption>> LoadClaimedInvitationOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        string[] registeredVpnAddresses = await dbContext.RemoteServers
            .Select(server => server.VpnAddress)
            .ToArrayAsync(cancellationToken);

        return await dbContext.Invitations
            .Where(invitation => invitation.InviteStatus == "Accepted" && !registeredVpnAddresses.Contains(invitation.VpnAddress))
            .OrderBy(invitation => invitation.VpnAddress)
            .Select(invitation => new ClaimedInvitationOption(
                invitation.Id,
                invitation.VpnAddress,
                invitation.RemoteApiKey,
                invitation.UsedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task AddFromInvitationAsync(int invitationId, string port, CancellationToken cancellationToken = default)
    {
        if (invitationId <= 0)
        {
            throw new InvalidOperationException("Claimed invitation is required.");
        }

        string normalizedPort = string.IsNullOrWhiteSpace(port)
            ? throw new InvalidOperationException("Port is required.")
            : port.Trim();
        if (!int.TryParse(normalizedPort, out int parsedPort) || parsedPort <= 0)
        {
            throw new InvalidOperationException("Port is invalid.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        InvitationEntity invitation = await dbContext.Invitations
            .FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
            ?? throw new InvalidOperationException("Claimed invitation was not found.");

        if (!string.Equals(invitation.InviteStatus, "Accepted", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invitation must be claimed before adding the server.");
        }

        bool exists = await dbContext.RemoteServers.AnyAsync(
            server => server.VpnAddress == invitation.VpnAddress,
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Server is already registered.");
        }

        dbContext.RemoteServers.Add(new RemoteServerEntity
        {
            VpnAddress = invitation.VpnAddress,
            Port = parsedPort,
            InviteStatus = "Accepted",
            ValidationStatus = invitation.ValidationStatus,
            LastSeenAtUtc = invitation.LastSeenAtUtc,
            ApiKey = invitation.RemoteApiKey
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
