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

        builder.HasData(new { MapId = 1, Name = "Bind" });
        builder.HasData(new { MapId = 2, Name = "Haven" });
        builder.HasData(new { MapId = 3, Name = "Split" });
        builder.HasData(new { MapId = 4, Name = "Ascent" });
        builder.HasData(new { MapId = 5, Name = "Icebox" });
        builder.HasData(new { MapId = 6, Name = "Breeze" });
        builder.HasData(new { MapId = 7, Name = "Fracture" });
        builder.HasData(new { MapId = 8, Name = "Pearl" });
        builder.HasData(new { MapId = 9, Name = "Lotus" });
        builder.HasData(new { MapId = 10, Name = "Sunset" });
        builder.HasData(new { MapId = 11, Name = "Abyss" });
        builder.HasData(new { MapId = 12, Name = "Corrode" });
        builder.HasData(new { MapId = 13, Name = "Summit" });

        builder.HasIndex(map => map.Name).IsUnique().HasDatabaseName("UX_Maps_Name");
    }
}