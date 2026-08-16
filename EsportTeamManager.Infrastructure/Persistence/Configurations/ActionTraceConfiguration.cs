using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ActionTraceConfiguration : IEntityTypeConfiguration<ActionTrace>
{
    public void Configure(EntityTypeBuilder<ActionTrace> builder)
    {
        builder.ToTable("ActionTraces", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_ActionTraces_Outcome", "\"Outcome\" IN ('Succeeded', 'Failed')");
            tableBuilder.HasCheckConstraint("CK_ActionTraces_Expiration", "\"ExpiresAtUtc\" > \"OccurredAtUtc\"");
        });

        builder.HasKey(actionTrace => actionTrace.ActionTraceId);

        builder.Property(actionTrace => actionTrace.ActionTraceId).ValueGeneratedNever();
        builder.Property(actionTrace => actionTrace.ActionCode).HasMaxLength(60).IsRequired();
        builder.Property(actionTrace => actionTrace.ObjectType).HasMaxLength(60).IsRequired();
        builder.Property(actionTrace => actionTrace.ObjectIdentifier).HasMaxLength(64);
        builder.Property(actionTrace => actionTrace.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(actionTrace => actionTrace.OccurredAtUtc).IsRequired();
        builder.Property(actionTrace => actionTrace.ExpiresAtUtc).IsRequired();

        builder.HasIndex(actionTrace => actionTrace.TeamId).HasDatabaseName("IX_ActionTraces_TeamId");
        builder.HasIndex(actionTrace => actionTrace.ActionCode).HasDatabaseName("IX_ActionTraces_ActionCode");
        builder.HasIndex(actionTrace => actionTrace.OccurredAtUtc).HasDatabaseName("IX_ActionTraces_OccurredAtUtc");
        builder.HasIndex(actionTrace => actionTrace.ExpiresAtUtc).HasDatabaseName("IX_ActionTraces_ExpiresAtUtc");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(actionTrace => actionTrace.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Team>().WithMany().HasForeignKey(actionTrace => actionTrace.TeamId).OnDelete(DeleteBehavior.SetNull);
    }
}