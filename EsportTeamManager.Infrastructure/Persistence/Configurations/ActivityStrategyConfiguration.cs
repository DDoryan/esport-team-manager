using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ActivityStrategyConfiguration : IEntityTypeConfiguration<ActivityStrategy>
{
    public void Configure(EntityTypeBuilder<ActivityStrategy> builder)
    {
        builder.ToTable("ActivityStrategies");

        builder.HasKey(activityStrategy => new { activityStrategy.ActivityId, activityStrategy.StrategyId });

        builder.HasOne<TeamActivity>().WithMany().HasForeignKey(activityStrategy => activityStrategy.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Strategy>().WithMany().HasForeignKey(activityStrategy => activityStrategy.StrategyId).OnDelete(DeleteBehavior.Cascade);
    }
}