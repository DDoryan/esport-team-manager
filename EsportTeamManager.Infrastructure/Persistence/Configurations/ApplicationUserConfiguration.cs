using EsportTeamManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsportTeamManager.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_AspNetUsers_AccountStatus", "\"AccountStatus\" IN ('PendingConfirmation', 'Active', 'Suspended')");
            tableBuilder.HasCheckConstraint("CK_AspNetUsers_PasswordResetEmailCount", "\"PasswordResetEmailCount\" >= 0 AND \"PasswordResetEmailCount\" <= 3");
        });

        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(user => user.UserName).HasMaxLength(26).IsRequired();
        builder.Property(user => user.NormalizedUserName).HasMaxLength(26).IsRequired();

        builder.Property(user => user.Pseudo).HasMaxLength(20).IsRequired();
        builder.Property(user => user.Tag).HasMaxLength(5).IsRequired();
        builder.Property(user => user.PendingEmail).HasMaxLength(254);
        builder.Property(user => user.NormalizedPendingEmail).HasMaxLength(254);
        builder.Property(user => user.AccountStatus).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(user => user.MinimumAgeDeclaredAtUtc).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.Property(user => user.PasswordResetEmailCount).IsRequired();

        builder.HasIndex(user => user.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
        builder.HasIndex(user => user.NormalizedUserName).IsUnique().HasDatabaseName("UserNameIndex");
        builder.HasIndex(user => user.NormalizedPendingEmail).HasDatabaseName("IX_AspNetUsers_NormalizedPendingEmail");
    }
}