using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class FormerMemberConfiguration : IEntityTypeConfiguration<FormerMember>
{
    public void Configure(EntityTypeBuilder<FormerMember> builder)
    {
        builder.ToTable("FormerMembers", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_FormerMembers_LocalNumber", "\"LocalNumber\" > 0");
        });

        builder.HasKey(formerMember => formerMember.FormerMemberId);

        builder.Property(formerMember => formerMember.FormerMemberId).ValueGeneratedNever();
        builder.Property(formerMember => formerMember.TeamId).IsRequired();
        builder.Property(formerMember => formerMember.LocalNumber).IsRequired();
        builder.Property(formerMember => formerMember.CreatedAtUtc).IsRequired();

        builder.HasIndex(formerMember => new { formerMember.TeamId, formerMember.LocalNumber }).IsUnique().HasDatabaseName("UX_FormerMembers_TeamId_LocalNumber");

        builder.HasOne<Team>().WithMany().HasForeignKey(formerMember => formerMember.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}