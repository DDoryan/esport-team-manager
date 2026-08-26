using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Invitations_Status", "\"Status\" IN ('Pending', 'Accepted', 'Refused', 'Cancelled')");
            tableBuilder.HasCheckConstraint("CK_Invitations_Resolution", "(\"Status\" = 'Pending' AND \"ResolvedAtUtc\" IS NULL) OR (\"Status\" <> 'Pending' AND \"ResolvedAtUtc\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_Invitations_CreatedMembership", "(\"Status\" = 'Accepted' AND \"CreatedMembershipId\" IS NOT NULL) OR (\"Status\" <> 'Accepted' AND \"CreatedMembershipId\" IS NULL)");
            tableBuilder.HasCheckConstraint("CK_Invitations_ResolvedAtUtc", "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"CreatedAtUtc\"");
        });

        builder.HasKey(invitation => invitation.InvitationId);

        builder.Property(invitation => invitation.InvitationId).ValueGeneratedNever();
        builder.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(invitation => invitation.CreatedAtUtc).IsRequired();

        builder.HasIndex(invitation => new { invitation.SenderUserId, invitation.CreatedAtUtc }).HasDatabaseName("IX_Invitations_SenderUserId_CreatedAtUtc");
        builder.HasIndex(invitation => new { invitation.RecipientUserId, invitation.Status }).HasDatabaseName("IX_Invitations_RecipientUserId_Status");
        builder.HasIndex(invitation => new { invitation.TeamId, invitation.Status }).HasDatabaseName("IX_Invitations_TeamId_Status");
        builder.HasIndex(invitation => new { invitation.TeamId, invitation.RecipientUserId }).IsUnique().HasFilter("\"Status\" = 'Pending'").HasDatabaseName("UX_Invitations_PendingRecipient");

        builder.HasOne<Team>().WithMany().HasForeignKey(invitation => invitation.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.SenderUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TeamRole>().WithMany().HasForeignKey(invitation => invitation.ProposedTeamRoleId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(invitation => invitation.CreatedMembershipId).OnDelete(DeleteBehavior.SetNull);
    }
}