using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ActivityTypeConfiguration : IEntityTypeConfiguration<ActivityType>
{
    public void Configure(EntityTypeBuilder<ActivityType> builder)
    {
        builder.ToTable("ActivityTypes", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_ActivityTypes_SystemOwnership", "(\"IsSystem\" = TRUE AND \"TeamId\" IS NULL) OR (\"IsSystem\" = FALSE AND \"TeamId\" IS NOT NULL)");
        });

        builder.HasKey(activityType => activityType.ActivityTypeId);

        builder.Property(activityType => activityType.ActivityTypeId).ValueGeneratedNever();
        builder.Property(activityType => activityType.Code).HasMaxLength(30).IsRequired();
        builder.Property(activityType => activityType.Label).HasMaxLength(50).IsRequired();
        builder.Property(activityType => activityType.IsSystem).IsRequired();

        builder.HasData(new { ActivityTypeId = 1, Code = "Pracc", Label = "Pracc", IsSystem = true, TeamId = (Guid?)null });
        builder.HasData(new { ActivityTypeId = 2, Code = "OfficialMatch", Label = "Match officiel", IsSystem = true, TeamId = (Guid?)null });
        builder.HasData(new { ActivityTypeId = 3, Code = "Meeting", Label = "Réunion", IsSystem = true, TeamId = (Guid?)null });
        builder.HasData(new { ActivityTypeId = 4, Code = "VodReview", Label = "Review VOD", IsSystem = true, TeamId = (Guid?)null });

        builder.HasIndex(activityType => activityType.Code).IsUnique().HasFilter("\"TeamId\" IS NULL").HasDatabaseName("UX_ActivityTypes_SystemCode");
        builder.HasIndex(activityType => new { activityType.TeamId, activityType.Code }).IsUnique().HasFilter("\"TeamId\" IS NOT NULL").HasDatabaseName("UX_ActivityTypes_TeamId_Code");

        builder.HasOne<Team>().WithMany().HasForeignKey(activityType => activityType.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}