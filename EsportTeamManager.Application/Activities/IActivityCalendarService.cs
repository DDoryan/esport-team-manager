namespace EsportTeamManager.Application.Activities;

public interface IActivityCalendarService
{
    Task<IReadOnlyCollection<CalendarActivitySummary>> GetForPeriodAsync(Guid teamId, DateTimeOffset periodStart, DateTimeOffset periodEnd, bool includeCancelled, CancellationToken cancellationToken = default);
}