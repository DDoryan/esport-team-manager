using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class LegalDocumentVersionConfiguration : IEntityTypeConfiguration<LegalDocumentVersion>
{
    public void Configure(EntityTypeBuilder<LegalDocumentVersion> builder)
    {
        builder.ToTable("LegalDocumentVersions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_LegalDocumentVersions_DocumentType", "\"DocumentType\" IN ('TermsOfService', 'PrivacyPolicy')");
        });

        builder.HasKey(version => version.LegalDocumentVersionId);

        builder.Property(version => version.LegalDocumentVersionId).ValueGeneratedNever();
        builder.Property(version => version.DocumentType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(version => version.VersionNumber).HasMaxLength(20).IsRequired();
        builder.Property(version => version.PublishedAtUtc).IsRequired();
        builder.Property(version => version.RequiresAcceptance).IsRequired();

        builder.HasIndex(version => new { version.DocumentType, version.VersionNumber }).IsUnique().HasDatabaseName("UX_LegalDocumentVersions_DocumentType_VersionNumber");
    }
}