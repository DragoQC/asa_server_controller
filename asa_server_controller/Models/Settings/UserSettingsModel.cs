namespace asa_server_controller.Models.Settings;

public sealed class UserSettingsModel
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsPasswordLoginEnabled { get; set; }
    public bool IsEmailLoginEnabled { get; set; }
    public bool IsTwoFactorLoginEnabled { get; set; }
    public int EnabledLoginMethodCount { get; set; }
    public bool IsSmtpConfigured { get; set; }
    public bool HasTwoFactorSecret { get; set; }
    public string TwoFactorSecret { get; set; } = string.Empty;
    public string TwoFactorOtpAuthUri { get; set; } = string.Empty;
}
