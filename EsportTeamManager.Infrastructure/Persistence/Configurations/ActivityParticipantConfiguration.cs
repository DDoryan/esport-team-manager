using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ActivityParticipantConfiguration : IEntityTypeConfiguration<ActivityParticipant>
{
    public void Configure(EntityTypeBuilder<ActivityParticipant> builder)
    {
        builder.ToTable("ActivityParticipants", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_ActivityParticipants_Attendance", "\"Attendance\" IS NULL OR \"Attendance\" IN ('Present', 'Absent')");
        });

        builder.HasKey(participant => new { participant.ActivityId, participant.TeamMembershipId });

        builder.Property(participant => participant.Attendance).HasConversion<string>().HasMaxLength(10);

        builder.HasIndex(participant => participant.TeamMembershipId).HasDatabaseName("IX_ActivityParticipants_TeamMembershipId");

        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(participant => participant.TeamMembershipId).OnDelete(DeleteBehavior.NoAction);
    }
}