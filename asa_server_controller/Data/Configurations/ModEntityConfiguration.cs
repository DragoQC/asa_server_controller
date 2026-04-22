using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class ModEntityConfiguration : IEntityTypeConfiguration<ModEntity>
{
    public void Configure(EntityTypeBuilder<ModEntity> builder)
    {
        builder.ToTable("Mods");

        builder.HasKey(mod => mod.Id);

        builder.Property(mod => mod.Id)
            .ValueGeneratedOnAdd();

        builder.Property(mod => mod.CreatedAtUtc);

        builder.Property(mod => mod.ModifiedAtUtc);

        builder.Property(mod => mod.CurseForgeModId)
            .IsRequired();

        builder.Property(mod => mod.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(mod => mod.Summary)
            .HasMaxLength(2048);

        builder.Property(mod => mod.Slug)
            .HasMaxLength(256);

        builder.Property(mod => mod.WebsiteUrl)
            .HasMaxLength(1024);

        builder.Property(mod => mod.LogoUrl)
            .HasMaxLength(1024);

        builder.Property(mod => mod.DateModifiedUtc);

        builder.HasIndex(mod => mod.CurseForgeModId)
            .IsUnique();
    }
}
