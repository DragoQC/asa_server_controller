using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class RemoteServerModEntityConfiguration : IEntityTypeConfiguration<RemoteServerModEntity>
{
    public void Configure(EntityTypeBuilder<RemoteServerModEntity> builder)
    {
        builder.ToTable("RemoteServerMods");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Id)
            .ValueGeneratedOnAdd();

        builder.Property(link => link.CreatedAtUtc);

        builder.Property(link => link.ModifiedAtUtc);

        builder.Property(link => link.RemoteServerId)
            .IsRequired();

        builder.Property(link => link.ModEntityId)
            .IsRequired();

        builder.HasIndex(link => new { link.RemoteServerId, link.ModEntityId })
            .IsUnique();

        builder.HasOne<RemoteServerEntity>()
            .WithMany()
            .HasForeignKey(link => link.RemoteServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ModEntity>()
            .WithMany()
            .HasForeignKey(link => link.ModEntityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
