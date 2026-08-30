namespace EsportTeamManager.Application.Activities;

public interface IActivityCalendarService
{
    Task<IReadOnlyCollection<ActivityCalendarParticipantOption>> GetParticipantOptionsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CalendarActivitySummary>> GetForPeriodAsync(Guid teamId, DateTimeOffset periodStart, DateTimeOffset periodEnd, ActivityCalendarFilter filter, CancellationToken cancellationToken = default);
}