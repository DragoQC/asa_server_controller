using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Settings;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Services;

public sealed class EmailSettingsService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const int SettingsId = 1;

    public async Task<EmailSettingsModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        EmailSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);

        return new EmailSettingsModel
        {
            SmtpHost = settings.SmtpHost,
            SmtpPort = settings.SmtpPort > 0 ? settings.SmtpPort : 587,
            SmtpUsername = settings.SmtpUsername,
            SmtpPassword = settings.SmtpPassword,
            FromEmail = settings.FromEmail,
            FromName = settings.FromName
        };
    }

    public async Task SaveAsync(EmailSettingsModel model, CancellationToken cancellationToken = default)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        EmailSettingsEntity settings = await GetOrCreateSettingsEntityAsync(dbContext, cancellationToken);
        settings.SmtpHost = model.SmtpHost?.Trim() ?? string.Empty;
        settings.SmtpPort = model.SmtpPort;
        settings.SmtpUsername = model.SmtpUsername?.Trim() ?? string.Empty;
        settings.SmtpPassword = model.SmtpPassword?.Trim() ?? string.Empty;
        settings.FromEmail = model.FromEmail?.Trim() ?? string.Empty;
        settings.FromName = model.FromName?.Trim() ?? string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        EmailSettingsModel model = await LoadAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(model.SmtpHost) &&
               model.SmtpPort > 0 &&
               !string.IsNullOrWhiteSpace(model.SmtpUsername) &&
               !string.IsNullOrWhiteSpace(model.SmtpPassword) &&
               !string.IsNullOrWhiteSpace(model.FromEmail) &&
               !string.IsNullOrWhiteSpace(model.FromName);
    }

    private static async Task<EmailSettingsEntity> GetOrCreateSettingsEntityAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        EmailSettingsEntity? settings = await dbContext.EmailSettings
            .FirstOrDefaultAsync(entity => entity.Id == SettingsId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new EmailSettingsEntity
        {
            Id = SettingsId,
            SmtpHost = string.Empty,
            SmtpPort = 587,
            SmtpUsername = string.Empty,
            SmtpPassword = string.Empty,
            FromEmail = string.Empty,
            FromName = string.Empty
        };

        dbContext.EmailSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
