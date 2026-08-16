using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ActivityLinkConfiguration : IEntityTypeConfiguration<ActivityLink>
{
    public void Configure(EntityTypeBuilder<ActivityLink> builder)
    {
        builder.ToTable("ActivityLinks", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_ActivityLinks_Url", "LOWER(\"Url\") LIKE 'http://%' OR LOWER(\"Url\") LIKE 'https://%'");
        });

        builder.HasKey(activityLink => activityLink.ActivityLinkId);

        builder.Property(activityLink => activityLink.ActivityLinkId).ValueGeneratedNever();
        builder.Property(activityLink => activityLink.Name).HasMaxLength(100).IsRequired();
        builder.Property(activityLink => activityLink.Url).HasMaxLength(2048).IsRequired();

        builder.HasIndex(activityLink => activityLink.ActivityId).HasDatabaseName("IX_ActivityLinks_ActivityId");

        builder.HasOne<TeamActivity>().WithMany().HasForeignKey(activityLink => activityLink.ActivityId).OnDelete(DeleteBehavior.Cascade);
    }
}