using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class TeamRoleConfiguration : IEntityTypeConfiguration<TeamRole>
{
    public void Configure(EntityTypeBuilder<TeamRole> builder)
    {
        builder.ToTable("TeamRoles", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_TeamRoles_SystemOwnership", "(\"IsSystem\" = TRUE AND \"TeamId\" IS NULL) OR (\"IsSystem\" = FALSE AND \"TeamId\" IS NOT NULL)");
        });

        builder.HasKey(teamRole => teamRole.TeamRoleId);

        builder.Property(teamRole => teamRole.TeamRoleId).ValueGeneratedNever();
        builder.Property(teamRole => teamRole.Code).HasMaxLength(30).IsRequired();
        builder.Property(teamRole => teamRole.Label).HasMaxLength(50).IsRequired();
        builder.Property(teamRole => teamRole.IsSystem).IsRequired();

        builder.HasIndex(teamRole => teamRole.Code).IsUnique().HasFilter("\"TeamId\" IS NULL").HasDatabaseName("UX_TeamRoles_SystemCode");
        builder.HasIndex(teamRole => new { teamRole.TeamId, teamRole.Code }).IsUnique().HasFilter("\"TeamId\" IS NOT NULL").HasDatabaseName("UX_TeamRoles_TeamId_Code");

        builder.HasOne<Team>().WithMany().HasForeignKey(teamRole => teamRole.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}