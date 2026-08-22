using EsportTeamManager.Application.Activities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Activities;

public sealed class ActivityCalendarService : IActivityCalendarService
{
    private readonly ApplicationDbContext _context;

    public ActivityCalendarService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<CalendarActivitySummary>> GetForPeriodAsync(Guid teamId, DateTimeOffset periodStart, DateTimeOffset periodEnd, bool includeCancelled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("The team identifier cannot be empty.", nameof(teamId));
        }

        DateTimeOffset normalizedStart = periodStart.ToUniversalTime();
        DateTimeOffset normalizedEnd = periodEnd.ToUniversalTime();

        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("The period end must be later than its start.", nameof(periodEnd));
        }

        return await _context.TeamActivities
            .AsNoTracking()
            .Where(activity => activity.TeamId == teamId)
            .Where(activity => activity.PlannedStartUtc < normalizedEnd && activity.PlannedEndUtc > normalizedStart)
            .Where(activity => includeCancelled || activity.Status != ActivityStatus.Cancelled)
            .OrderBy(activity => activity.PlannedStartUtc)
            .ThenBy(activity => activity.PlannedEndUtc)
            .Select(activity => new CalendarActivitySummary(
                activity.ActivityId,
                activity.ActivityType.Code,
                activity.ActivityType.Label,
                activity.Subtitle,
                activity.MatchDetail == null ? null : activity.MatchDetail.OpponentName,
                activity.PlannedStartUtc,
                activity.PlannedEndUtc,
                activity.TimeZoneId,
                activity.Status))
            .ToListAsync(cancellationToken);
    }
}