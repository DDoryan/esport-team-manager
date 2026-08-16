using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Notifications_Source", "(\"InvitationId\" IS NOT NULL AND \"OwnershipTransferId\" IS NULL) OR (\"InvitationId\" IS NULL AND \"OwnershipTransferId\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_Notifications_ReadAtUtc", "\"ReadAtUtc\" IS NULL OR \"ReadAtUtc\" >= \"CreatedAtUtc\"");
        });

        builder.HasKey(notification => notification.NotificationId);

        builder.Property(notification => notification.NotificationId).ValueGeneratedNever();
        builder.Property(notification => notification.CreatedAtUtc).IsRequired();

        builder.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc }).HasDatabaseName("IX_Notifications_RecipientUserId_ReadAtUtc");
        builder.HasIndex(notification => notification.InvitationId).IsUnique().HasFilter("\"InvitationId\" IS NOT NULL").HasDatabaseName("UX_Notifications_InvitationId");
        builder.HasIndex(notification => notification.OwnershipTransferId).IsUnique().HasFilter("\"OwnershipTransferId\" IS NOT NULL").HasDatabaseName("UX_Notifications_OwnershipTransferId");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(notification => notification.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Invitation>().WithMany().HasForeignKey(notification => notification.InvitationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OwnershipTransfer>().WithMany().HasForeignKey(notification => notification.OwnershipTransferId).OnDelete(DeleteBehavior.Cascade);
    }
}