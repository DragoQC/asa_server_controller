using Microsoft.AspNetCore.SignalR.Client;

namespace asa_server_controller.Models.Servers;

public sealed record RemoteHubRegistration(
    HubConnection Connection,
    string BaseUrl,
    string ApiKey);
