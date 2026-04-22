namespace asa_server_controller.Models.Servers;

public sealed record PublicServerModItem(
    long ModId,
    string Name,
    string Summary,
    string WebsiteUrl,
    string LogoUrl);
