using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Persistence;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<ActionTrace> ActionTraces => Set<ActionTrace>();

    public DbSet<ActivityLink> ActivityLinks => Set<ActivityLink>();

    public DbSet<ActivityParticipant> ActivityParticipants => Set<ActivityParticipant>();

    public DbSet<ActivityStrategy> ActivityStrategies => Set<ActivityStrategy>();

    public DbSet<ActivityType> ActivityTypes => Set<ActivityType>();

    public DbSet<FormerMember> FormerMembers => Set<FormerMember>();

    public DbSet<ImageFile> ImageFiles => Set<ImageFile>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    public DbSet<LegalDocumentVersion> LegalDocumentVersions => Set<LegalDocumentVersion>();

    public DbSet<Map> Maps => Set<Map>();

    public DbSet<MatchDetail> MatchDetails => Set<MatchDetail>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<OwnershipTransfer> OwnershipTransfers => Set<OwnershipTransfer>();

    public DbSet<ReservedIdentity> ReservedIdentities => Set<ReservedIdentity>();

    public DbSet<Strategy> Strategies => Set<Strategy>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamActivity> TeamActivities => Set<TeamActivity>();

    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    public DbSet<TeamRole> TeamRoles => Set<TeamRole>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}