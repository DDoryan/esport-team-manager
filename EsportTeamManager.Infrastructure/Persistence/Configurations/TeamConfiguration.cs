using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Teams_DeletedMemberCounter", "\"DeletedMemberCounter\" >= 0");
        });

        builder.HasKey(team => team.TeamId);

        builder.Property(team => team.TeamId).ValueGeneratedNever();
        builder.Property(team => team.Name).HasMaxLength(50).IsRequired();
        builder.Property(team => team.Tag).HasMaxLength(6);
        builder.Property(team => team.Description).HasMaxLength(500);
        builder.Property(team => team.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(team => team.DeletedMemberCounter).HasDefaultValue(0).IsRequired();
        builder.Property(team => team.CreatedAtUtc).IsRequired();

        builder.HasIndex(team => team.OwnerUserId).HasDatabaseName("IX_Teams_OwnerUserId");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(team => team.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}