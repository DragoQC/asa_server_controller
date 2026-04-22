using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class NfsShareInviteEntityConfiguration : IEntityTypeConfiguration<NfsShareInviteEntity>
{
    public void Configure(EntityTypeBuilder<NfsShareInviteEntity> builder)
    {
        builder.ToTable("NfsShareInvites");

        builder.HasKey(invite => invite.Id);

        builder.Property(invite => invite.Id)
            .ValueGeneratedOnAdd();

        builder.Property(invite => invite.CreatedAtUtc);

        builder.Property(invite => invite.ModifiedAtUtc);

        builder.Property(invite => invite.RemoteServerId)
            .IsRequired();

        builder.Property(invite => invite.InviteKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(invite => invite.InviteLink)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(invite => invite.UsedAtUtc);

        builder.HasIndex(invite => invite.InviteKey)
            .IsUnique();

        builder.HasOne(invite => invite.RemoteServer)
            .WithMany(server => server.NfsShareInvites)
            .HasForeignKey(invite => invite.RemoteServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
