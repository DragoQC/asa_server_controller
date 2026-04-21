using managerwebapp.Data.Entities;
using managerwebapp.Models.Auth;
using managerwebapp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace managerwebapp.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AuthService authService) : Controller
{
    [HttpPost("login")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginRequest request, CancellationToken cancellationToken)
    {
        string identifier = request.Username ?? string.Empty;
        string action = ResolveLoginAction(request);

        if (action == "email-request")
        {
            IdentityResult requestResult = await authService.RequestEmailLoginCodeAsync(identifier, cancellationToken);
            if (!requestResult.Succeeded)
            {
                string requestError = Uri.EscapeDataString(string.Join(' ', requestResult.Errors.Select(error => error.Description)));
                return LocalRedirect($"/admin/login?error={requestError}");
            }

            return LocalRedirect("/admin/login?message=If%20email%20login%20is%20enabled,%20a%20code%20was%20sent.");
        }

        User? user = action switch
        {
            "email" => await authService.AuthenticateWithEmailCodeAsync(identifier, request.EmailCode ?? string.Empty, cancellationToken),
            "totp" => await authService.AuthenticateWithTwoFactorAsync(identifier, request.TwoFactorCode ?? string.Empty, cancellationToken),
            _ => await authService.AuthenticateAsync(identifier, request.Password ?? string.Empty, cancellationToken)
        };

        if (user is null)
        {
            return LocalRedirect("/admin/login?error=Login%20failed.");
        }

        await authService.SignInAsync(HttpContext, user, isPersistent: true, cancellationToken);

        bool mustChangePassword = await authService.MustChangePasswordAsync(user.UserName);
        return LocalRedirect(mustChangePassword ? "/admin/reset-password?firstLogin=true" : "/admin/dashboard");
    }

    [HttpPost("logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await authService.SignOutAsync(HttpContext);
        return LocalRedirect("/admin/login?message=Logged%20out.");
    }

    private static string ResolveLoginAction(LoginRequest request)
    {
        string? requestedAction = request.Action?.Trim();
        if (string.Equals(requestedAction, "email-request", StringComparison.Ordinal))
        {
            return "email-request";
        }

        if (!string.IsNullOrWhiteSpace(request.TwoFactorCode))
        {
            return "totp";
        }

        if (!string.IsNullOrWhiteSpace(request.EmailCode))
        {
            return "email";
        }

        return "password";
    }
}
