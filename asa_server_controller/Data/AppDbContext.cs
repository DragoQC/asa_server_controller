using asa_server_controller.Data.Configurations;
using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace asa_server_controller.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ClusterSettingsEntity> ClusterSettings => Set<ClusterSettingsEntity>();
    public DbSet<CurseForgeSettingsEntity> CurseForgeSettings => Set<CurseForgeSettingsEntity>();
    public DbSet<EmailSettingsEntity> EmailSettings => Set<EmailSettingsEntity>();
    public DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();
    public DbSet<LoginMethodTypeEntity> LoginMethodTypes => Set<LoginMethodTypeEntity>();
    public DbSet<ModEntity> Mods => Set<ModEntity>();
    public DbSet<NfsShareInviteEntity> NfsShareInvites => Set<NfsShareInviteEntity>();
    public DbSet<RemoteServerEntity> RemoteServers => Set<RemoteServerEntity>();
    public DbSet<RemoteServerModEntity> RemoteServerMods => Set<RemoteServerModEntity>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserLoginMethodEntity> UserLoginMethods => Set<UserLoginMethodEntity>();
    public DbSet<VpnServerSettingsEntity> VpnServerSettings => Set<VpnServerSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new ClusterSettingsEntityConfiguration());
        builder.ApplyConfiguration(new CurseForgeSettingsEntityConfiguration());
        builder.ApplyConfiguration(new EmailSettingsEntityConfiguration());
        builder.ApplyConfiguration(new InvitationEntityConfiguration());
        builder.ApplyConfiguration(new LoginMethodTypeEntityConfiguration());
        builder.ApplyConfiguration(new ModEntityConfiguration());
        builder.ApplyConfiguration(new NfsShareInviteEntityConfiguration());
        builder.ApplyConfiguration(new RemoteServerEntityConfiguration());
        builder.ApplyConfiguration(new RemoteServerModEntityConfiguration());
        builder.ApplyConfiguration(new RoleConfiguration());
        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new UserLoginMethodEntityConfiguration());
        builder.ApplyConfiguration(new VpnServerSettingsEntityConfiguration());
    }
}
