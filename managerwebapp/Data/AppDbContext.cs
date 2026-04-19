using managerwebapp.Data.Configurations;
using managerwebapp.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace managerwebapp.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<ApplicationUser>(options)
{
    public DbSet<ClusterSettingsEntity> ClusterSettings => Set<ClusterSettingsEntity>();
    public DbSet<CurseForgeSettingsEntity> CurseForgeSettings => Set<CurseForgeSettingsEntity>();
    public DbSet<EmailSettingsEntity> EmailSettings => Set<EmailSettingsEntity>();
    public DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();
    public DbSet<ModEntity> Mods => Set<ModEntity>();
    public DbSet<NfsShareInviteEntity> NfsShareInvites => Set<NfsShareInviteEntity>();
    public DbSet<RemoteServerEntity> RemoteServers => Set<RemoteServerEntity>();
    public DbSet<RemoteServerModEntity> RemoteServerMods => Set<RemoteServerModEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ClusterSettingsEntityConfiguration());
        builder.ApplyConfiguration(new CurseForgeSettingsEntityConfiguration());
        builder.ApplyConfiguration(new EmailSettingsEntityConfiguration());
        builder.ApplyConfiguration(new InvitationEntityConfiguration());
        builder.ApplyConfiguration(new ModEntityConfiguration());
        builder.ApplyConfiguration(new NfsShareInviteEntityConfiguration());
        builder.ApplyConfiguration(new RemoteServerEntityConfiguration());
        builder.ApplyConfiguration(new RemoteServerModEntityConfiguration());
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default)
                {
                    entry.Entity.CreatedAtUtc = now;
                }

                entry.Entity.ModifiedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAtUtc).IsModified = false;
                entry.Entity.ModifiedAtUtc = now;
            }
        }
    }
}
