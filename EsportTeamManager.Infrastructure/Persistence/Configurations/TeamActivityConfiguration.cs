using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class TeamActivityConfiguration : IEntityTypeConfiguration<TeamActivity>
{
    private static readonly ValueConverter<DateTimeOffset, DateTime> UtcDateTimeConverter = new(dateTime => dateTime.UtcDateTime, dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)));

    public void Configure(EntityTypeBuilder<TeamActivity> builder)
    {
        builder.ToTable("Activities", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Activities_Schedule", "\"PlannedEndUtc\" > \"PlannedStartUtc\"");
            tableBuilder.HasCheckConstraint("CK_Activities_Status", "\"Status\" IN ('Planned', 'Completed', 'Cancelled')");
            tableBuilder.HasCheckConstraint("CK_Activities_UpdatedAtUtc", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
            tableBuilder.HasCheckConstraint("CK_Activities_CancellationReason", "\"Status\" = 'Cancelled' OR \"CancellationReason\" IS NULL");
        });

        builder.HasKey(activity => activity.ActivityId);

        builder.Property(activity => activity.ActivityId).ValueGeneratedNever();
        builder.Property(activity => activity.Subtitle).HasMaxLength(100);
        builder.Property(activity => activity.Description).HasMaxLength(2000);
        builder.Property(activity => activity.Report).HasMaxLength(5000);
        builder.Property(activity => activity.PlannedStartUtc).HasConversion(UtcDateTimeConverter).IsRequired();
        builder.Property(activity => activity.PlannedEndUtc).HasConversion(UtcDateTimeConverter).IsRequired();
        builder.Property(activity => activity.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(activity => activity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(activity => activity.CancellationReason).HasMaxLength(500);
        builder.Property(activity => activity.CreatedAtUtc).HasConversion(UtcDateTimeConverter).IsRequired();
        builder.Property(activity => activity.UpdatedAtUtc).HasConversion(UtcDateTimeConverter).IsRequired();

        builder.Ignore(activity => activity.RequiresScores);

        builder.HasIndex(activity => new { activity.TeamId, activity.PlannedStartUtc }).HasDatabaseName("IX_Activities_TeamId_PlannedStartUtc");
        builder.HasIndex(activity => new { activity.TeamId, activity.Status }).HasDatabaseName("IX_Activities_TeamId_Status");
        builder.HasIndex(activity => activity.ActivityTypeId).HasDatabaseName("IX_Activities_ActivityTypeId");

        builder.HasOne<Team>().WithMany().HasForeignKey(activity => activity.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(activity => activity.ActivityType).WithMany().HasForeignKey(activity => activity.ActivityTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(activity => activity.CreatedByMembershipId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(activity => activity.MatchDetail).WithOne().HasForeignKey<MatchDetail>(matchDetail => matchDetail.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(activity => activity.Participants).WithOne().HasForeignKey(participant => participant.ActivityId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(activity => activity.Participants).HasField("_participants").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}