using System.ComponentModel.DataAnnotations;

namespace managerwebapp.Models.Settings;

public sealed class EmailSettingsModel
{
    [StringLength(256)]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [StringLength(256)]
    public string SmtpUsername { get; set; } = string.Empty;

    [StringLength(512)]
    public string SmtpPassword { get; set; } = string.Empty;

    [StringLength(256)]
    public string FromEmail { get; set; } = string.Empty;

    [StringLength(256)]
    public string FromName { get; set; } = string.Empty;
}
