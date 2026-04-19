using managerwebapp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace managerwebapp.Data.Configurations;

public sealed class RemoteServerEntityConfiguration : IEntityTypeConfiguration<RemoteServerEntity>
{
    public void Configure(EntityTypeBuilder<RemoteServerEntity> builder)
    {
        builder.ToTable("RemoteServers");

        builder.HasKey(remoteServer => remoteServer.Id);

        builder.Property(remoteServer => remoteServer.Id)
            .ValueGeneratedOnAdd();

        builder.Property(remoteServer => remoteServer.CreatedAtUtc);

        builder.Property(remoteServer => remoteServer.ModifiedAtUtc);

        builder.Property(remoteServer => remoteServer.VpnAddress)
            .HasColumnName("IpAddress")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(remoteServer => remoteServer.Port)
            .IsRequired(false);

        builder.Property(remoteServer => remoteServer.InviteStatus)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(remoteServer => remoteServer.ValidationStatus)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(remoteServer => remoteServer.LastSeenAtUtc);

        builder.Property(remoteServer => remoteServer.ApiKey)
            .HasColumnName("ApiKeyHash")
            .HasMaxLength(512)
            .IsRequired();

        builder.HasMany(remoteServer => remoteServer.Invitations)
            .WithOne(invitation => invitation.RemoteServer)
            .HasForeignKey(invitation => invitation.RemoteServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
