using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using asa_server_controller.Data.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Identity;

namespace asa_server_controller.Services;

public sealed class EmailLoginCodeService(IMemoryCache memoryCache, EmailSettingsService emailSettingsService)
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly EmailSettingsService _emailSettingsService = emailSettingsService;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public async Task<IdentityResult> SendCodeAsync(User user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "User email is missing."
            });
        }

        if (!await _emailSettingsService.IsConfiguredAsync(cancellationToken))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "SMTP settings must be configured before sending an email code."
            });
        }

        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _memoryCache.Set(GetCacheKey(user.Id), code, CodeLifetime);

        Models.Settings.EmailSettingsModel settings = await _emailSettingsService.LoadAsync(cancellationToken);

        try
        {
            using MailMessage message = new()
            {
                From = new MailAddress(settings.FromEmail, settings.FromName),
                Subject = "Your ASA Manager login code",
                Body = $"Your login code is: {code}\n\nThis code expires in 10 minutes."
            };

            message.To.Add(user.Email);

            using SmtpClient smtpClient = new(settings.SmtpHost, settings.SmtpPort)
            {
                Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword),
                EnableSsl = true
            };

            cancellationToken.ThrowIfCancellationRequested();
            await smtpClient.SendMailAsync(message, cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception exception)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = $"Failed to send email code. {exception.Message}"
            });
        }
    }

    public bool ValidateCode(User user, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (!_memoryCache.TryGetValue(GetCacheKey(user.Id), out string? expectedCode) ||
            string.IsNullOrWhiteSpace(expectedCode) ||
            !string.Equals(expectedCode, code.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        _memoryCache.Remove(GetCacheKey(user.Id));
        return true;
    }

    private static string GetCacheKey(int userId)
    {
        return $"email-login-code:{userId}";
    }
}
