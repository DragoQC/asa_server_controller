using System.Security.Claims;
using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Services;

public sealed class AuthService(AppDbContext dbContext, IPasswordHasher<User> passwordHasher, EmailSettingsService emailSettingsService, TotpService totpService, EmailLoginCodeService emailLoginCodeService)
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly EmailSettingsService _emailSettingsService = emailSettingsService;
    private readonly TotpService _totpService = totpService;
    private readonly EmailLoginCodeService _emailLoginCodeService = emailLoginCodeService;
    private static readonly string[] DefaultLoginMethodTypes = ["Password", "Email", "TwoFactor"];
    private static readonly string[] DefaultRoles = ["admin", "guest"];
    private const string TotpIssuer = "ASA Server Manager";

    public async Task EnsureDefaultAdminUserAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync(cancellationToken);
        await EnsureLoginMethodTypesAsync(cancellationToken);

        User? user = await _dbContext.Users
            .Include(candidate => candidate.Role)
            .FirstOrDefaultAsync(candidate => candidate.UserName == "admin", cancellationToken);

        if (user is not null)
        {
            await EnsureLoginMethodSetupAsync(user, cancellationToken);
            return;
        }

        Role adminRole = await _dbContext.Roles
            .FirstAsync(role => role.Name == "admin", cancellationToken);

        User adminUser = new()
        {
            UserName = "admin",
            Email = "admin@local",
            RoleId = adminRole.Id,
            Role = adminRole
        };

        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, "admin");
        _dbContext.Users.Add(adminUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureLoginMethodSetupAsync(adminUser, cancellationToken);
    }

    public async Task<bool> MustChangePasswordAsync(ClaimsPrincipal principal)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return false;
        }

        return await MustChangePasswordAsync(user);
    }

    public async Task<bool> MustChangePasswordAsync(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        User? user = await _dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.UserName == username);
        if (user is null)
        {
            return false;
        }

        return await MustChangePasswordAsync(user);
    }

    public async Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal principal, string newPassword)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Current user was not found."
            });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

        await _dbContext.SaveChangesAsync();
        return IdentityResult.Success;
    }

    public async Task<UserSettingsModel?> LoadUserSettingsAsync(ClaimsPrincipal principal)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return null;
        }

        Dictionary<string, int> loginMethodTypeIds = await _dbContext.LoginMethodTypes
            .ToDictionaryAsync(type => type.Name, type => type.Id);

        List<int> enabledMethodIds = await _dbContext.UserLoginMethods
            .Where(link => link.UserId == user.Id)
            .Where(link => link.IsEnabled)
            .Select(link => link.LoginMethodTypeId)
            .ToListAsync();

        return new UserSettingsModel
        {
            UserName = user.UserName,
            Email = user.Email,
            IsPasswordLoginEnabled = loginMethodTypeIds.TryGetValue("Password", out int passwordTypeId) && enabledMethodIds.Contains(passwordTypeId),
            IsEmailLoginEnabled = loginMethodTypeIds.TryGetValue("Email", out int emailTypeId) && enabledMethodIds.Contains(emailTypeId),
            IsTwoFactorLoginEnabled = loginMethodTypeIds.TryGetValue("TwoFactor", out int twoFactorTypeId) && enabledMethodIds.Contains(twoFactorTypeId),
            EnabledLoginMethodCount = enabledMethodIds.Count,
            IsSmtpConfigured = await _emailSettingsService.IsConfiguredAsync(),
            HasTwoFactorSecret = !string.IsNullOrWhiteSpace(user.TwoFactorSecret),
            TwoFactorSecret = user.TwoFactorSecret,
            TwoFactorOtpAuthUri = string.IsNullOrWhiteSpace(user.TwoFactorSecret)
                ? string.Empty
                : _totpService.BuildOtpAuthUri(TotpIssuer, user.UserName, user.TwoFactorSecret)
        };
    }

    public async Task<IdentityResult> SaveEmailAsync(ClaimsPrincipal principal, string email)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Current user was not found."
            });
        }

        string normalizedEmail = email.Trim();
        bool emailInUse = await _dbContext.Users
            .AnyAsync(candidate => candidate.Id != user.Id && candidate.Email == normalizedEmail);

        if (emailInUse)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Email is already used by another account."
            });
        }

        user.Email = normalizedEmail;
        await _dbContext.SaveChangesAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> EnableEmailLoginAsync(ClaimsPrincipal principal)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Current user was not found."
            });
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Set an email before enabling email login."
            });
        }

        if (!await _emailSettingsService.IsConfiguredAsync())
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "SMTP settings must be configured before enabling email login."
            });
        }

        LoginMethodTypeEntity emailType = await _dbContext.LoginMethodTypes
            .FirstAsync(type => type.Name == "Email");

        UserLoginMethodEntity? existingLink = await _dbContext.UserLoginMethods
            .FirstOrDefaultAsync(link => link.UserId == user.Id && link.LoginMethodTypeId == emailType.Id);

        if (existingLink is null)
        {
            _dbContext.UserLoginMethods.Add(new UserLoginMethodEntity
            {
                UserId = user.Id,
                User = user,
                LoginMethodTypeId = emailType.Id,
                LoginMethodType = emailType,
                IsEnabled = true
            });
        }
        else
        {
            existingLink.IsEnabled = true;
        }

        await _dbContext.SaveChangesAsync();

        return IdentityResult.Success;
    }

    public async Task<UserSettingsModel?> GenerateTwoFactorSetupAsync(ClaimsPrincipal principal)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            user.TwoFactorSecret = _totpService.GenerateSecret();
            await _dbContext.SaveChangesAsync();
        }

        return await LoadUserSettingsAsync(principal);
    }

    public async Task<IdentityResult> EnableTwoFactorAsync(ClaimsPrincipal principal, string code)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Current user was not found."
            });
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Generate a two-factor secret first."
            });
        }

        if (!_totpService.ValidateCode(user.TwoFactorSecret, code))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Two-factor code is invalid."
            });
        }

        LoginMethodTypeEntity type = await _dbContext.LoginMethodTypes
            .FirstAsync(entity => entity.Name == "TwoFactor");

        UserLoginMethodEntity? existingLink = await _dbContext.UserLoginMethods
            .FirstOrDefaultAsync(link => link.UserId == user.Id && link.LoginMethodTypeId == type.Id);

        if (existingLink is null)
        {
            _dbContext.UserLoginMethods.Add(new UserLoginMethodEntity
            {
                UserId = user.Id,
                User = user,
                LoginMethodTypeId = type.Id,
                LoginMethodType = type,
                IsEnabled = true
            });
        }
        else
        {
            existingLink.IsEnabled = true;
        }

        await _dbContext.SaveChangesAsync();

        return IdentityResult.Success;
    }

    public async Task<bool> IsTwoFactorEnabledAsync(User user)
    {
        LoginMethodTypeEntity type = await _dbContext.LoginMethodTypes
            .FirstAsync(entity => entity.Name == "TwoFactor");

        return await _dbContext.UserLoginMethods
            .AnyAsync(link => link.UserId == user.Id && link.LoginMethodTypeId == type.Id && link.IsEnabled);
    }

    public bool ValidateTwoFactorCode(User user, string? code)
    {
        return !string.IsNullOrWhiteSpace(user.TwoFactorSecret) &&
               _totpService.ValidateCode(user.TwoFactorSecret, code ?? string.Empty);
    }

    public async Task<IdentityResult> RequestEmailLoginCodeAsync(string identifier, CancellationToken cancellationToken = default)
    {
        User? user = await FindByIdentifierAsync(identifier, cancellationToken);
        if (user is null)
        {
            return IdentityResult.Success;
        }

        LoginMethodAvailability availability = await LoadLoginMethodAvailabilityAsync(identifier, cancellationToken);
        if (!availability.EmailEnabled)
        {
            return IdentityResult.Success;
        }

        IdentityResult result = await _emailLoginCodeService.SendCodeAsync(user, cancellationToken);
        return result.Succeeded ? result : IdentityResult.Success;
    }

    public async Task<User?> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        string normalizedIdentifier = identifier.Trim();
        User? user = await _dbContext.Users
            .Include(candidate => candidate.Role)
            .FirstOrDefaultAsync(candidate => candidate.UserName == normalizedIdentifier || candidate.Email == normalizedIdentifier, cancellationToken);

        if (user is null)
        {
            return null;
        }

        LoginMethodAvailability availability = await LoadLoginMethodAvailabilityAsync(user.UserName, cancellationToken);
        if (!availability.PasswordEnabled)
        {
            return null;
        }

        PasswordVerificationResult verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<User?> AuthenticateWithEmailCodeAsync(string identifier, string code, CancellationToken cancellationToken = default)
    {
        User? user = await FindByIdentifierAsync(identifier, cancellationToken);
        if (user is null)
        {
            return null;
        }

        LoginMethodAvailability availability = await LoadLoginMethodAvailabilityAsync(identifier, cancellationToken);
        if (!availability.EmailEnabled)
        {
            return null;
        }

        return _emailLoginCodeService.ValidateCode(user, code) ? user : null;
    }

    public async Task<User?> AuthenticateWithTwoFactorAsync(string identifier, string code, CancellationToken cancellationToken = default)
    {
        User? user = await FindByIdentifierAsync(identifier, cancellationToken);
        if (user is null)
        {
            return null;
        }

        LoginMethodAvailability availability = await LoadLoginMethodAvailabilityAsync(identifier, cancellationToken);
        if (!availability.TwoFactorEnabled)
        {
            return null;
        }

        return ValidateTwoFactorCode(user, code) ? user : null;
    }

    public async Task SignInAsync(HttpContext httpContext, User user, bool isPersistent, CancellationToken cancellationToken = default)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.Name)
        ];

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        ClaimsPrincipal principal = new(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = isPersistent
            });
    }

    public Task SignOutAsync(HttpContext httpContext)
    {
        return httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private Task<bool> MustChangePasswordAsync(User user)
    {
        bool isDefaultAdmin = string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase);
        if (!isDefaultAdmin)
        {
            return Task.FromResult(false);
        }

        PasswordVerificationResult verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "admin");
        return Task.FromResult(verificationResult != PasswordVerificationResult.Failed);
    }

    private async Task EnsureLoginMethodSetupAsync(User user, CancellationToken cancellationToken)
    {
        LoginMethodTypeEntity passwordType = await _dbContext.LoginMethodTypes
            .FirstAsync(type => type.Name == "Password", cancellationToken);

        UserLoginMethodEntity? existingLink = await _dbContext.UserLoginMethods.FirstOrDefaultAsync(
            link => link.UserId == user.Id && link.LoginMethodTypeId == passwordType.Id,
            cancellationToken);

        if (existingLink is null)
        {
            _dbContext.UserLoginMethods.Add(new UserLoginMethodEntity
            {
                UserId = user.Id,
                User = user,
                LoginMethodTypeId = passwordType.Id,
                LoginMethodType = passwordType,
                IsEnabled = true
            });
        }
        else
        {
            existingLink.IsEnabled = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLoginMethodTypesAsync(CancellationToken cancellationToken)
    {
        List<string> existingNames = await _dbContext.LoginMethodTypes
            .Select(type => type.Name)
            .ToListAsync(cancellationToken);

        string[] missingNames = DefaultLoginMethodTypes
            .Where(name => !existingNames.Contains(name, StringComparer.Ordinal))
            .ToArray();

        if (missingNames.Length == 0)
        {
            return;
        }

        foreach (string missingName in missingNames)
        {
            _dbContext.LoginMethodTypes.Add(new LoginMethodTypeEntity
            {
                Name = missingName
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRolesAsync(CancellationToken cancellationToken)
    {
        List<string> existingNames = await _dbContext.Roles
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);

        string[] missingNames = DefaultRoles
            .Where(name => !existingNames.Contains(name, StringComparer.Ordinal))
            .ToArray();

        if (missingNames.Length == 0)
        {
            return;
        }

        foreach (string missingName in missingNames)
        {
            _dbContext.Roles.Add(new Role
            {
                Name = missingName
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User?> FindUserAsync(ClaimsPrincipal principal)
    {
        string? userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out int userId))
        {
            return null;
        }

        return await _dbContext.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Id == userId);
    }

    public async Task<LoginMethodAvailability> LoadLoginMethodAvailabilityAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return new LoginMethodAvailability(false, false, false);
        }

        string normalizedIdentifier = identifier.Trim();
        User? user = await _dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.UserName == normalizedIdentifier || candidate.Email == normalizedIdentifier, cancellationToken);

        if (user is null)
        {
            return new LoginMethodAvailability(false, false, false);
        }

        Dictionary<string, int> typeIds = await _dbContext.LoginMethodTypes
            .ToDictionaryAsync(type => type.Name, type => type.Id, cancellationToken);

        List<int> enabledIds = await _dbContext.UserLoginMethods
            .Where(link => link.UserId == user.Id && link.IsEnabled)
            .Select(link => link.LoginMethodTypeId)
            .ToListAsync(cancellationToken);

        return new LoginMethodAvailability(
            typeIds.TryGetValue("Password", out int passwordTypeId) && enabledIds.Contains(passwordTypeId),
            typeIds.TryGetValue("Email", out int emailTypeId) && enabledIds.Contains(emailTypeId),
            typeIds.TryGetValue("TwoFactor", out int twoFactorTypeId) && enabledIds.Contains(twoFactorTypeId));
    }

    public async Task<IdentityResult> SetLoginMethodEnabledAsync(ClaimsPrincipal principal, string methodName, bool isEnabled)
    {
        User? user = await FindUserAsync(principal);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Current user was not found."
            });
        }

        LoginMethodTypeEntity type = await _dbContext.LoginMethodTypes
            .FirstAsync(entity => entity.Name == methodName);

        UserLoginMethodEntity? link = await _dbContext.UserLoginMethods
            .FirstOrDefaultAsync(entity => entity.UserId == user.Id && entity.LoginMethodTypeId == type.Id);

        if (isEnabled)
        {
            if (methodName == "Email")
            {
                return await EnableEmailLoginAsync(principal);
            }

            if (methodName == "TwoFactor")
            {
                if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Description = "Generate and verify a two-factor secret first."
                    });
                }
            }

            if (methodName == "Password" && string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "Set a password before enabling password login."
                });
            }

            if (link is null)
            {
                _dbContext.UserLoginMethods.Add(new UserLoginMethodEntity
                {
                    UserId = user.Id,
                    User = user,
                    LoginMethodTypeId = type.Id,
                    LoginMethodType = type,
                    IsEnabled = true
                });
            }
            else
            {
                link.IsEnabled = true;
            }

            await _dbContext.SaveChangesAsync();
            return IdentityResult.Success;
        }

        List<UserLoginMethodEntity> enabledLinks = await _dbContext.UserLoginMethods
            .Where(entity => entity.UserId == user.Id && entity.IsEnabled)
            .ToListAsync();

        if (enabledLinks.Count <= 1 && enabledLinks.Any(entity => entity.LoginMethodTypeId == type.Id))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "At least one login method must stay enabled."
            });
        }

        if (link is not null)
        {
            link.IsEnabled = false;
            await _dbContext.SaveChangesAsync();
        }

        return IdentityResult.Success;
    }

    public async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        string normalizedIdentifier = identifier.Trim();
        return await _dbContext.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.UserName == normalizedIdentifier || user.Email == normalizedIdentifier, cancellationToken);
    }
}
