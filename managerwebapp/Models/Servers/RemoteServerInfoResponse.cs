using System.Text.Json.Serialization;

namespace managerwebapp.Models.Servers;

public sealed record RemoteServerInfoResponse(
    bool Success,
    string ServerName,
    string MapName,
    int MaxPlayers,
    int? GamePort,
    DateTimeOffset CheckedAtUtc,
    [property: JsonPropertyName("modIds")] List<string>? ModIds);
