using asa_server_controller.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace asa_server_controller.Data.Configurations;

public sealed class InvitationEntityConfiguration : IEntityTypeConfiguration<InvitationEntity>
{
    public void Configure(EntityTypeBuilder<InvitationEntity> builder)
    {
        builder.ToTable("Invitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Id)
            .ValueGeneratedOnAdd();

        builder.Property(invitation => invitation.CreatedAtUtc);

        builder.Property(invitation => invitation.ModifiedAtUtc);

        builder.Property(invitation => invitation.RemoteUrl)
            .HasMaxLength(512);

        builder.Property(invitation => invitation.ClusterId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(invitation => invitation.RemoteServerId)
            .IsRequired();

        builder.Property(invitation => invitation.OneTimeVpnKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(invitation => invitation.InviteLink)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(invitation => invitation.InviteStatus)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(invitation => invitation.ValidationStatus)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(invitation => invitation.UsedAtUtc);

        builder.Property(invitation => invitation.LastSeenAtUtc);

        builder.HasIndex(invitation => invitation.OneTimeVpnKey)
            .IsUnique();
    }
}
