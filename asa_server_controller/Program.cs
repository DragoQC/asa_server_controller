using asa_server_controller.Data;
using asa_server_controller.Data.Entities;
using asa_server_controller.Models.Settings;
using asa_server_controller.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using asa_server_controller.Components;

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
builder.Services.AddScoped<LogsService>();
builder.Services.AddScoped<MapNameService>();
builder.Services.AddScoped<NfsService>();
builder.Services.AddSingleton<ModsEventsService>();
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
builder.Services.AddScoped<GameServerInfoService>();
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

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
