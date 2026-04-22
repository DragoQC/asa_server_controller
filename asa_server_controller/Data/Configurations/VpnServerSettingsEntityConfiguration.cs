using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class VpnServerSettingsEntityConfiguration : IEntityTypeConfiguration<VpnServerSettingsEntity>
{
    public void Configure(EntityTypeBuilder<VpnServerSettingsEntity> builder)
    {
        builder.ToTable("VpnServerSettings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();

        builder.Property(settings => settings.CreatedAtUtc);

        builder.Property(settings => settings.ModifiedAtUtc);

        builder.Property(settings => settings.Endpoint)
            .HasMaxLength(256);

        builder.Property(settings => settings.AllowedIps)
            .HasMaxLength(256);

        builder.Property(settings => settings.PersistentKeepalive)
            .HasMaxLength(64);

        builder.Property(settings => settings.PresharedKey)
            .HasMaxLength(256);
    }
}
