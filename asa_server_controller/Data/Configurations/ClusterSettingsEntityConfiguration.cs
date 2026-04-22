using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class ClusterSettingsEntityConfiguration : IEntityTypeConfiguration<ClusterSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ClusterSettingsEntity> builder)
    {
        builder.ToTable("ClusterSettings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();

        builder.Property(settings => settings.CreatedAtUtc);

        builder.Property(settings => settings.ModifiedAtUtc);

        builder.Property(settings => settings.ClusterId)
            .HasMaxLength(256);
    }
}
