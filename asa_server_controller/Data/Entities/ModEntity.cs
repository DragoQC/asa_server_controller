namespace asa_server_controller.Data.Entities;

public sealed class ModEntity : BaseEntity
{
    public long CurseForgeModId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public long DownloadCount { get; set; }
    public DateTimeOffset? DateModifiedUtc { get; set; }
}
