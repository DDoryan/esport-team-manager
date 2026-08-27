using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class OwnershipTransferConfiguration : IEntityTypeConfiguration<OwnershipTransfer>
{
    public void Configure(EntityTypeBuilder<OwnershipTransfer> builder)
    {
        builder.ToTable("OwnershipTransfers", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_OwnershipTransfers_Status", "\"Status\" IN ('Pending', 'Accepted', 'Refused', 'Cancelled')");
            tableBuilder.HasCheckConstraint("CK_OwnershipTransfers_Memberships", "\"InitiatorMembershipId\" <> \"RecipientMembershipId\"");
            tableBuilder.HasCheckConstraint("CK_OwnershipTransfers_Resolution", "(\"Status\" = 'Pending' AND \"ResolvedAtUtc\" IS NULL) OR (\"Status\" <> 'Pending' AND \"ResolvedAtUtc\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_OwnershipTransfers_ResolvedAtUtc", "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"CreatedAtUtc\"");
        });

        builder.HasKey(transfer => transfer.OwnershipTransferId);

        builder.Property(transfer => transfer.OwnershipTransferId).ValueGeneratedNever();
        builder.Property(transfer => transfer.Status).HasConversion<string>().HasMaxLength(16).IsRequired().IsConcurrencyToken();
        builder.Property(transfer => transfer.CreatedAtUtc).IsRequired();

        builder.HasIndex(transfer => new { transfer.TeamId, transfer.Status }).HasDatabaseName("IX_OwnershipTransfers_TeamId_Status");
        builder.HasIndex(transfer => transfer.TeamId).IsUnique().HasFilter("\"Status\" = 'Pending'").HasDatabaseName("UX_OwnershipTransfers_PendingTeam");

        builder.HasOne<Team>().WithMany().HasForeignKey(transfer => transfer.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(transfer => transfer.InitiatorMembershipId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(transfer => transfer.RecipientMembershipId).OnDelete(DeleteBehavior.NoAction);
    }
}