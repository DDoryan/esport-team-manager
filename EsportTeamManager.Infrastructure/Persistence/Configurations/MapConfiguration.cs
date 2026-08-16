using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class MapConfiguration : IEntityTypeConfiguration<Map>
{
    public void Configure(EntityTypeBuilder<Map> builder)
    {
        builder.ToTable("Maps");

        builder.HasKey(map => map.MapId);

        builder.Property(map => map.MapId).ValueGeneratedNever();
        builder.Property(map => map.Name).HasMaxLength(50).IsRequired();

        builder.HasIndex(map => map.Name).IsUnique().HasDatabaseName("UX_Maps_Name");
    }
}