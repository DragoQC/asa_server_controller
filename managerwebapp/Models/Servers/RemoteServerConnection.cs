namespace managerwebapp.Models.Servers;

public sealed record RemoteServerConnection(
    int Id,
    string VpnAddress,
    int Port,
    string ApiKey)
{
    public string BaseUrl => $"http://{VpnAddress}:{Port}";
}
