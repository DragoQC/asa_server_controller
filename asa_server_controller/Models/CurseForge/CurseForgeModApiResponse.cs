using System.Text.Json.Serialization;

namespace asa_server_controller.Models.CurseForge;

public sealed record CurseForgeModApiResponse(
    [property: JsonPropertyName("data")] CurseForgeModData? Data);

public sealed record CurseForgeModData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("downloadCount")] long DownloadCount,
    [property: JsonPropertyName("dateModified")] DateTimeOffset? DateModified,
    [property: JsonPropertyName("links")] CurseForgeModLinks? Links,
    [property: JsonPropertyName("logo")] CurseForgeModAsset? Logo);

public sealed record CurseForgeModLinks(
    [property: JsonPropertyName("websiteUrl")] string? WebsiteUrl);

public sealed record CurseForgeModAsset(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("thumbnailUrl")] string? ThumbnailUrl);
