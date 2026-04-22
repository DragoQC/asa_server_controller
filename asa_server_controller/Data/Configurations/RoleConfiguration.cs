using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedOnAdd();

        builder.Property(role => role.CreatedAtUtc);

        builder.Property(role => role.ModifiedAtUtc);

        builder.Property(role => role.Name)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(role => role.Name)
            .IsUnique();
    }
}
