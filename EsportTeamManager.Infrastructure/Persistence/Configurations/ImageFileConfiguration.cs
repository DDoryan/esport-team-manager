using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ImageFileConfiguration : IEntityTypeConfiguration<ImageFile>
{
    public void Configure(EntityTypeBuilder<ImageFile> builder)
    {
        builder.ToTable("ImageFiles", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_ImageFiles_Owner", "(\"TeamLogoForTeamId\" IS NOT NULL AND \"StrategyImageForStrategyId\" IS NULL) OR (\"TeamLogoForTeamId\" IS NULL AND \"StrategyImageForStrategyId\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_ImageFiles_MediaType", "\"MediaType\" IN ('image/png', 'image/jpeg', 'image/webp')");
            tableBuilder.HasCheckConstraint("CK_ImageFiles_FileSizeBytes", "\"FileSizeBytes\" > 0");
            tableBuilder.HasCheckConstraint("CK_ImageFiles_WidthPixels", "\"WidthPixels\" > 0");
            tableBuilder.HasCheckConstraint("CK_ImageFiles_HeightPixels", "\"HeightPixels\" > 0");
        });

        builder.HasKey(imageFile => imageFile.ImageFileId);

        builder.Property(imageFile => imageFile.ImageFileId).ValueGeneratedNever();
        builder.Property(imageFile => imageFile.InternalFileName).HasMaxLength(100).IsRequired();
        builder.Property(imageFile => imageFile.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(imageFile => imageFile.MediaType).HasMaxLength(32).IsRequired();
        builder.Property(imageFile => imageFile.FileSizeBytes).IsRequired();
        builder.Property(imageFile => imageFile.WidthPixels).IsRequired();
        builder.Property(imageFile => imageFile.HeightPixels).IsRequired();
        builder.Property(imageFile => imageFile.OptimizedStorageKey).HasMaxLength(500).IsRequired();
        builder.Property(imageFile => imageFile.ThumbnailStorageKey).HasMaxLength(500).IsRequired();
        builder.Property(imageFile => imageFile.CreatedAtUtc).IsRequired();

        builder.HasIndex(imageFile => imageFile.InternalFileName).IsUnique().HasDatabaseName("UX_ImageFiles_InternalFileName");
        builder.HasIndex(imageFile => imageFile.OptimizedStorageKey).IsUnique().HasDatabaseName("UX_ImageFiles_OptimizedStorageKey");
        builder.HasIndex(imageFile => imageFile.ThumbnailStorageKey).IsUnique().HasDatabaseName("UX_ImageFiles_ThumbnailStorageKey");
        builder.HasIndex(imageFile => imageFile.TeamLogoForTeamId).IsUnique().HasFilter("\"TeamLogoForTeamId\" IS NOT NULL").HasDatabaseName("UX_ImageFiles_TeamLogoForTeamId");
        builder.HasIndex(imageFile => imageFile.StrategyImageForStrategyId).IsUnique().HasFilter("\"StrategyImageForStrategyId\" IS NOT NULL").HasDatabaseName("UX_ImageFiles_StrategyImageForStrategyId");

        builder.HasOne<Team>().WithMany().HasForeignKey(imageFile => imageFile.TeamLogoForTeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Strategy>().WithMany().HasForeignKey(imageFile => imageFile.StrategyImageForStrategyId).OnDelete(DeleteBehavior.Cascade);
    }
}