using managerwebapp.Data;
using managerwebapp.Data.Entities;
using managerwebapp.Models.Settings;
using managerwebapp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using managerwebapp.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/admin/login";
        options.Cookie.Name = "asa-control-auth";
    });
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

string databasePath = Path.Combine(builder.Environment.ContentRootPath, "Data", "managerwebapp.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? builder.Environment.ContentRootPath);
string connectionString = $"Data Source={databasePath}";

builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlite(connectionString)
        .AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.AddDbContextFactory<AppDbContext>(
    (services, options) => options.UseSqlite(connectionString)
        .AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>()),
    ServiceLifetime.Scoped);

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailLoginCodeService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ClusterSettingsService>();
builder.Services.AddScoped<EmailSettingsService>();
builder.Services.AddScoped<NfsService>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<InvitationEventsService>();
builder.Services.AddHttpClient<CurseForgeService>(client =>
{
    client.BaseAddress = new Uri("https://api.curseforge.com");
});
builder.Services.AddScoped<ModsService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddHttpClient<RemoteAdminHttpClient>();
builder.Services.AddScoped<RemoteClusterService>();
builder.Services.AddScoped<RemoteServerService>();
builder.Services.AddScoped<RemoteManagerService>();
builder.Services.AddScoped<RemoteRconService>();
builder.Services.AddSingleton<RemoteServerHubClientService>();
builder.Services.AddSingleton<RemoteServerInfoService>();
builder.Services.AddSingleton<WireGuardInstallService>();
builder.Services.AddScoped<RemoteServerModsService>();
builder.Services.AddScoped<SudoService>();
builder.Services.AddScoped<VpnService>();
builder.Services.AddHostedService<InvitationMonitorService>();
builder.Services.AddHostedService(services => services.GetRequiredService<RemoteServerHubClientService>());
builder.Services.AddHostedService(services => services.GetRequiredService<RemoteServerInfoService>());

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    AuthService authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.EnsureDefaultAdminUserAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/login",
    async ([FromForm] LoginRequest request, HttpContext httpContext, AuthService authService) =>
    {
        string identifier = request.Username ?? string.Empty;
        string action = request.Action?.Trim() ?? "password";

        if (action == "email-request")
        {
            IdentityResult requestResult = await authService.RequestEmailLoginCodeAsync(identifier, httpContext.RequestAborted);
            if (!requestResult.Succeeded)
            {
                string requestError = Uri.EscapeDataString(string.Join(' ', requestResult.Errors.Select(error => error.Description)));
                return Results.LocalRedirect($"/admin/login?error={requestError}");
            }

            return Results.LocalRedirect("/admin/login?message=If%20email%20login%20is%20enabled,%20a%20code%20was%20sent.");
        }

        User? user = action switch
        {
            "email" => await authService.AuthenticateWithEmailCodeAsync(identifier, request.EmailCode ?? string.Empty, httpContext.RequestAborted),
            "totp" => await authService.AuthenticateWithTwoFactorAsync(identifier, request.TwoFactorCode ?? string.Empty, httpContext.RequestAborted),
            _ => await authService.AuthenticateAsync(identifier, request.Password ?? string.Empty, httpContext.RequestAborted)
        };

        if (user is null)
        {
            return Results.LocalRedirect("/admin/login?error=Login%20failed.");
        }

        await authService.SignInAsync(httpContext, user, isPersistent: true, httpContext.RequestAborted);

        bool mustChangePassword = await authService.MustChangePasswordAsync(user.UserName);

        return Results.LocalRedirect(mustChangePassword ? "/admin/reset-password?firstLogin=true" : "/admin/dashboard");
    })
    .DisableAntiforgery();

app.MapPost("/auth/logout",
    async (HttpContext httpContext, AuthService authService) =>
    {
        await authService.SignOutAsync(httpContext);
        return Results.LocalRedirect("/admin/login?message=Logged%20out.");
    })
    .DisableAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal sealed record LoginRequest(string? Username, string? Password, string? EmailCode, string? TwoFactorCode, string? Action);
