namespace asa_server_controller.Models.CurseForge;

public sealed record CachedMod(
    long ModId,
    string Name,
    string Summary,
    string WebsiteUrl,
    string LogoUrl,
    bool IsInUse,
    long DownloadCount,
    DateTimeOffset? DateModifiedUtc,
    bool IsMetadataResolved);
