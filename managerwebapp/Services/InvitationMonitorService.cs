using System.Net.NetworkInformation;
using managerwebapp.Data;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Services;

public sealed class InvitationMonitorService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(15));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await UpdateStatusesAsync(stoppingToken);
            }
            catch
            {
            }
        }
    }

    private async Task UpdateStatusesAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        IDbContextFactory<AppDbContext> dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<Data.Entities.InvitationEntity> invitations = await dbContext.Invitations
            .Where(invitation => invitation.InviteStatus == "Accepted")
            .ToListAsync(cancellationToken);

        foreach (Data.Entities.InvitationEntity invitation in invitations)
        {
            string ipAddress = GetIpAddress(invitation.VpnAddress);
            bool isOnline = await PingAddressAsync(ipAddress);
            invitation.ValidationStatus = isOnline ? "Online" : "Offline";
            invitation.LastSeenAtUtc = isOnline ? DateTimeOffset.UtcNow : invitation.LastSeenAtUtc;
        }

        if (invitations.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
