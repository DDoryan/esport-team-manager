using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("TeamMemberships", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_TeamMemberships_Holder", "(\"UserId\" IS NOT NULL AND \"FormerMemberId\" IS NULL) OR (\"UserId\" IS NULL AND \"FormerMemberId\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_TeamMemberships_Status", "\"Status\" IN ('Active', 'Left', 'Removed')");
            tableBuilder.HasCheckConstraint("CK_TeamMemberships_Closure", "(\"Status\" = 'Active' AND \"LeftAtUtc\" IS NULL) OR (\"Status\" IN ('Left', 'Removed') AND \"LeftAtUtc\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_TeamMemberships_LeftAtUtc", "\"LeftAtUtc\" IS NULL OR \"LeftAtUtc\" >= \"JoinedAtUtc\"");
        });

        builder.HasKey(membership => membership.TeamMembershipId);

        builder.Property(membership => membership.TeamMembershipId).ValueGeneratedNever();
        builder.Property(membership => membership.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(membership => membership.JoinedAtUtc).IsRequired();

        builder.HasIndex(membership => new { membership.TeamId, membership.Status }).HasDatabaseName("IX_TeamMemberships_TeamId_Status");
        builder.HasIndex(membership => new { membership.UserId, membership.Status }).HasDatabaseName("IX_TeamMemberships_UserId_Status");
        builder.HasIndex(membership => new { membership.TeamId, membership.UserId }).IsUnique().HasFilter("\"Status\" = 'Active' AND \"UserId\" IS NOT NULL").HasDatabaseName("UX_TeamMemberships_ActiveUser");

        builder.HasOne<Team>().WithMany().HasForeignKey(membership => membership.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(membership => membership.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FormerMember>().WithMany().HasForeignKey(membership => membership.FormerMemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TeamRole>().WithMany().HasForeignKey(membership => membership.TeamRoleId).OnDelete(DeleteBehavior.NoAction);
    }
}