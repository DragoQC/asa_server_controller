namespace asa_server_controller.Models.Auth;

public sealed class LoginRequest
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? EmailCode { get; init; }
    public string? TwoFactorCode { get; init; }
    public string? Action { get; init; }
}
