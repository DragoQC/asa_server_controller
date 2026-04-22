using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Cluster;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class ClusterSettingsService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const int SettingsId = 1;

    public async Task<ClusterSettingsModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ClusterSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);

        return new ClusterSettingsModel
        {
            ClusterId = settings.ClusterId
        };
    }

    public async Task<string> LoadRequiredClusterIdAsync(CancellationToken cancellationToken = default)
    {
        ClusterSettingsModel settings = await LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ClusterId))
        {
            throw new InvalidOperationException("Cluster ID is required before sending invitations.");
        }

        return settings.ClusterId.Trim();
    }

    public async Task SaveAsync(ClusterSettingsModel model, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ClusterSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);
        settings.ClusterId = model.ClusterId?.Trim() ?? string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public string GenerateClusterId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static async Task<ClusterSettingsEntity> GetOrCreateSettingsEntityAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        ClusterSettingsEntity? settings = await dbContext.ClusterSettings
            .FirstOrDefaultAsync(entity => entity.Id == SettingsId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new ClusterSettingsEntity
        {
            Id = SettingsId
        };

        dbContext.ClusterSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
