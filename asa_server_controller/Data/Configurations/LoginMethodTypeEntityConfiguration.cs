using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class LoginMethodTypeEntityConfiguration : IEntityTypeConfiguration<LoginMethodTypeEntity>
{
    public void Configure(EntityTypeBuilder<LoginMethodTypeEntity> builder)
    {
        builder.ToTable("LoginMethodTypes");

        builder.HasKey(type => type.Id);

        builder.Property(type => type.Id)
            .ValueGeneratedOnAdd();

        builder.Property(type => type.CreatedAtUtc);

        builder.Property(type => type.ModifiedAtUtc);

        builder.Property(type => type.Name)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(type => type.Name)
            .IsUnique();
    }
}
