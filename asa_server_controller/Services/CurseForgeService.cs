using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.CurseForge;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class CurseForgeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    HttpClient httpClient)
{
    private const int SettingsId = 1;

    public async Task<CurseForgeApiSettingsModel> LoadApiSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        CurseForgeSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);

        return new CurseForgeApiSettingsModel
        {
            ApiKey = settings.ApiKey
        };
    }

    public async Task SaveSettingsAsync(CurseForgeApiSettingsModel model, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        CurseForgeSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);
        settings.ApiKey = model.ApiKey?.Trim() ?? string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TestApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        CurseForgeModData _ = await GetModAsync(83374, apiKey, cancellationToken);
    }

    public async Task<CurseForgeModData> GetModAsync(long modId, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        CurseForgeSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);
        return await GetModAsync(modId, settings.ApiKey, cancellationToken);
    }

    private async Task<CurseForgeModData> GetModAsync(long modId, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("CurseForge API key is required.");
        }

        using HttpRequestMessage request = new(HttpMethod.Get, $"/v1/mods/{modId}");
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey.Trim());

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        CurseForgeModApiResponse? payload = await response.Content.ReadFromJsonAsync<CurseForgeModApiResponse>(cancellationToken);
        return payload?.Data ?? throw new InvalidOperationException($"CurseForge mod '{modId}' returned no data.");
    }

    public async Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        CurseForgeSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);
        return !string.IsNullOrWhiteSpace(settings.ApiKey);
    }

    private static async Task<CurseForgeSettingsEntity> GetOrCreateSettingsEntityAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        CurseForgeSettingsEntity? settings = await dbContext.CurseForgeSettings
            .FirstOrDefaultAsync(entity => entity.Id == SettingsId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new CurseForgeSettingsEntity
        {
            Id = SettingsId
        };

        dbContext.CurseForgeSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
