using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class MatchDetailConfiguration : IEntityTypeConfiguration<MatchDetail>
{
    public void Configure(EntityTypeBuilder<MatchDetail> builder)
    {
        builder.ToTable("MatchDetails", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_MatchDetails_CompleteScore", "(\"TeamScore\" IS NULL AND \"OpponentScore\" IS NULL) OR (\"TeamScore\" IS NOT NULL AND \"OpponentScore\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint("CK_MatchDetails_TeamScore", "\"TeamScore\" IS NULL OR \"TeamScore\" >= 0");
            tableBuilder.HasCheckConstraint("CK_MatchDetails_OpponentScore", "\"OpponentScore\" IS NULL OR \"OpponentScore\" >= 0");
        });

        builder.HasKey(matchDetail => matchDetail.ActivityId);

        builder.Property(matchDetail => matchDetail.ActivityId).ValueGeneratedNever();
        builder.Property(matchDetail => matchDetail.OpponentName).HasMaxLength(100);

        builder.Ignore(matchDetail => matchDetail.HasCompleteScore);
        builder.Ignore(matchDetail => matchDetail.Result);
    }
}