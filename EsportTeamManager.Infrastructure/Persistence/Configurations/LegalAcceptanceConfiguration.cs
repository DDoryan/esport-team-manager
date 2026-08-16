using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> builder)
    {
        builder.ToTable("LegalAcceptances");

        builder.HasKey(acceptance => new { acceptance.UserId, acceptance.LegalDocumentVersionId });

        builder.Property(acceptance => acceptance.AcceptedAtUtc).IsRequired();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(acceptance => acceptance.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LegalDocumentVersion>().WithMany().HasForeignKey(acceptance => acceptance.LegalDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}