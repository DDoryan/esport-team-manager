using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class StrategyConfiguration : IEntityTypeConfiguration<Strategy>
{
    public void Configure(EntityTypeBuilder<Strategy> builder)
    {
        builder.ToTable("Strategies", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Strategies_Side", "\"Side\" IN ('Attack', 'Defense')");
            tableBuilder.HasCheckConstraint("CK_Strategies_ExternalUrl", "\"ExternalUrl\" IS NULL OR LOWER(\"ExternalUrl\") LIKE 'http://%' OR LOWER(\"ExternalUrl\") LIKE 'https://%'");
            tableBuilder.HasCheckConstraint("CK_Strategies_UpdatedAtUtc", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });

        builder.HasKey(strategy => strategy.StrategyId);

        builder.Property(strategy => strategy.StrategyId).ValueGeneratedNever();
        builder.Property(strategy => strategy.Name).HasMaxLength(100).IsRequired();
        builder.Property(strategy => strategy.Side).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(strategy => strategy.Description).HasMaxLength(5000);
        builder.Property(strategy => strategy.ExternalUrl).HasMaxLength(2048);
        builder.Property(strategy => strategy.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(strategy => strategy.CreatedAtUtc).IsRequired();
        builder.Property(strategy => strategy.UpdatedAtUtc).IsRequired();

        builder.HasIndex(strategy => new { strategy.TeamId, strategy.IsActive }).HasDatabaseName("IX_Strategies_TeamId_IsActive");
        builder.HasIndex(strategy => strategy.MapId).HasDatabaseName("IX_Strategies_MapId");
        builder.HasIndex(strategy => strategy.Side).HasDatabaseName("IX_Strategies_Side");
        builder.HasIndex(strategy => strategy.UpdatedAtUtc).HasDatabaseName("IX_Strategies_UpdatedAtUtc");

        builder.HasOne<Team>().WithMany().HasForeignKey(strategy => strategy.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(strategy => strategy.CreatedByMembershipId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(strategy => strategy.Map).WithMany().HasForeignKey(strategy => strategy.MapId).OnDelete(DeleteBehavior.Restrict);
    }
}