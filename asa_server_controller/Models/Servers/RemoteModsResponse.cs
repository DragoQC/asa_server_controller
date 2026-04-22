using System.Text.Json.Serialization;

namespace asa_server_controller.Models.Servers;

public sealed record RemoteModsResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("modIds")] List<string>? ModIds);
