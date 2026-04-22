using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class UserLoginMethodEntityConfiguration : IEntityTypeConfiguration<UserLoginMethodEntity>
{
    public void Configure(EntityTypeBuilder<UserLoginMethodEntity> builder)
    {
        builder.ToTable("UserLoginMethods");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Id)
            .ValueGeneratedOnAdd();

        builder.Property(link => link.CreatedAtUtc);

        builder.Property(link => link.ModifiedAtUtc);

        builder.Property(link => link.UserId)
            .IsRequired();

        builder.Property(link => link.LoginMethodTypeId)
            .IsRequired();

        builder.Property(link => link.IsEnabled)
            .IsRequired();

        builder.HasIndex(link => new { link.UserId, link.LoginMethodTypeId })
            .IsUnique();

        builder.HasOne(link => link.User)
            .WithMany(user => user.LoginMethods)
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.LoginMethodType)
            .WithMany(type => type.UserLoginMethods)
            .HasForeignKey(link => link.LoginMethodTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
