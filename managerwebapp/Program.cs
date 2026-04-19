using managerwebapp.Data;
using managerwebapp.Services;
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
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

string databasePath = Path.Combine(builder.Environment.ContentRootPath, "Data", "managerwebapp.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? builder.Environment.ContentRootPath);
string connectionString = $"Data Source={databasePath}";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseSqlite(connectionString),
    ServiceLifetime.Scoped);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ClusterSettingsService>();
builder.Services.AddScoped<NfsConfigurationService>();
builder.Services.AddScoped<NfsShareService>();
builder.Services.AddHttpClient<CurseForgeService>(client =>
{
    client.BaseAddress = new Uri("https://api.curseforge.com");
});
builder.Services.AddScoped<InvitationService>();
builder.Services.AddHttpClient<RemoteAdminHttpClient>();
builder.Services.AddScoped<RemoteClusterService>();
builder.Services.AddScoped<RemoteServerService>();
builder.Services.AddScoped<RemoteManagerService>();
builder.Services.AddScoped<RemoteRconService>();
builder.Services.AddSingleton<RemoteServerHubClientService>();
builder.Services.AddSingleton<WireGuardInstallService>();
builder.Services.AddScoped<RemoteServerModsService>();
builder.Services.AddHostedService<RemoteServerModsRefreshService>();
builder.Services.AddScoped<SudoService>();
builder.Services.AddScoped<VpnConfigService>();
builder.Services.AddHostedService<InvitationMonitorService>();
builder.Services.AddHostedService(services => services.GetRequiredService<RemoteServerHubClientService>());

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
    async ([FromForm] LoginRequest request, SignInManager<ApplicationUser> signInManager, AuthService authService) =>
    {
        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.PasswordSignInAsync(
            request.Username ?? string.Empty,
            request.Password ?? string.Empty,
            isPersistent: true,
            lockoutOnFailure: false);

        bool mustChangePassword = result.Succeeded &&
                                  await authService.MustChangePasswordAsync(request.Username);

        return result.Succeeded
            ? Results.LocalRedirect(mustChangePassword ? "/admin/reset-password?firstLogin=true" : "/admin/dashboard")
            : Results.LocalRedirect("/admin/login?error=Invalid%20username%20or%20password.");
    })
    .DisableAntiforgery();

app.MapPost("/auth/logout",
    async (SignInManager<ApplicationUser> signInManager) =>
    {
        await signInManager.SignOutAsync();
        return Results.LocalRedirect("/admin/login?message=Logged%20out.");
    })
    .DisableAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal sealed record LoginRequest(string? Username, string? Password);
