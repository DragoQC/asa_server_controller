namespace managerwebapp.Models.Servers;

public sealed record RemoteServerConnection(
    int Id,
    string VpnAddress,
    int? Port,
    string ApiKey)
{
    public string BaseUrl => Port.HasValue
        ? $"http://{VpnAddress}:{Port.Value}"
        : throw new InvalidOperationException($"Remote server '{Id}' has no configured port.");
}
