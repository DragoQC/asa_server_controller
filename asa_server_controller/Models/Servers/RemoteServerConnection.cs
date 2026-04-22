using System.Net;

namespace asa_server_controller.Models.Servers;

public sealed record RemoteServerConnection(
    int Id,
    string VpnAddress,
    int? Port,
    string ApiKey)
{
    public string Host => NormalizeHost(VpnAddress);

    public string BaseUrl => BuildBaseUrl(Host, Port);

    private static string NormalizeHost(string vpnAddress)
    {
        string trimmed = vpnAddress.Trim();
        int slashIndex = trimmed.IndexOf('/');
        return slashIndex >= 0 ? trimmed[..slashIndex] : trimmed;
    }

    private static string BuildBaseUrl(string host, int? port)
    {
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Uri uri = new(host, UriKind.Absolute);
            UriBuilder builder = new(uri)
            {
                Port = port ?? uri.Port
            };

            return builder.Uri.ToString().TrimEnd('/');
        }

        bool isIpAddress = IPAddress.TryParse(host, out _);
        if (!port.HasValue)
        {
            if (isIpAddress)
            {
                throw new InvalidOperationException($"Remote server '{host}' has no configured port.");
            }

            return $"https://{host}";
        }

        string normalizedHost = host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
        string scheme = isIpAddress ? "http" : "https";
        return $"{scheme}://{normalizedHost}:{port.Value}";
    }
}
