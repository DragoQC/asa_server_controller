using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class CurseForgeSettingsEntityConfiguration : IEntityTypeConfiguration<CurseForgeSettingsEntity>
{
    public void Configure(EntityTypeBuilder<CurseForgeSettingsEntity> builder)
    {
        builder.ToTable("CurseForgeSettings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();

        builder.Property(settings => settings.CreatedAtUtc);

        builder.Property(settings => settings.ModifiedAtUtc);

        builder.Property(settings => settings.ApiKey)
            .HasMaxLength(512);
    }
}
