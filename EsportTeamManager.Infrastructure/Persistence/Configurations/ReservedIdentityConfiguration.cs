using EsportTeamManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ReservedIdentityConfiguration : IEntityTypeConfiguration<ReservedIdentity>
{
    public void Configure(EntityTypeBuilder<ReservedIdentity> builder)
    {
        builder.ToTable("ReservedIdentities", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_ReservedIdentities_IdentityHash", "LENGTH(\"IdentityHash\") = 64");
        });

        builder.HasKey(reservedIdentity => reservedIdentity.IdentityHash);

        builder.Property(reservedIdentity => reservedIdentity.IdentityHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(reservedIdentity => reservedIdentity.ReservedAtUtc).IsRequired();
    }
}