using EsportTeamManager.Application.Activities;
using EsportTeamManager.Domain.Entities;
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

    public async Task<IReadOnlyCollection<ActivityCalendarParticipantOption>> GetParticipantOptionsAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("The team identifier cannot be empty.", nameof(teamId));
        }

        IQueryable<TeamMembership> participantMemberships = _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .Where(membership => _context.ActivityParticipants.Any(participant => participant.TeamMembershipId == membership.TeamMembershipId));

        List<ActivityCalendarParticipantOption> userOptions = await participantMemberships
            .Where(membership => membership.UserId.HasValue)
            .Join(
                _context.Users.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    user.Id,
                    user.Pseudo,
                    user.Tag
                })
            .Distinct()
            .OrderBy(option => option.Pseudo)
            .ThenBy(option => option.Tag)
            .Select(option => new ActivityCalendarParticipantOption(option.Id, null, option.Pseudo + "#" + option.Tag))
            .ToListAsync(cancellationToken);

        List<ActivityCalendarParticipantOption> formerMemberOptions = await participantMemberships
            .Where(membership => membership.FormerMemberId.HasValue)
            .Join(
                _context.FormerMembers.AsNoTracking(),
                membership => membership.FormerMemberId,
                formerMember => formerMember.FormerMemberId,
                (membership, formerMember) => new
                {
                    formerMember.FormerMemberId,
                    formerMember.LocalNumber
                })
            .Distinct()
            .OrderBy(option => option.LocalNumber)
            .Select(option => new ActivityCalendarParticipantOption(null, option.FormerMemberId, "Utilisateur supprimé " + option.LocalNumber))
            .ToListAsync(cancellationToken);

        return userOptions
            .Concat(formerMemberOptions)
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<CalendarActivitySummary>> GetForPeriodAsync(Guid teamId, DateTimeOffset periodStart, DateTimeOffset periodEnd, ActivityCalendarFilter filter, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(filter);

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

        if (filter.TypeCodes.Count == 0 || filter.Statuses.Count == 0)
        {
            return Array.Empty<CalendarActivitySummary>();
        }

        IQueryable<TeamActivity> query = _context.TeamActivities
            .AsNoTracking()
            .Where(activity => activity.TeamId == teamId)
            .Where(activity => activity.PlannedStartUtc < normalizedEnd && activity.PlannedEndUtc > normalizedStart)
            .Where(activity => filter.TypeCodes.Contains(activity.ActivityType.Code))
            .Where(activity => filter.Statuses.Contains(activity.Status));

        if (filter.ParticipantUserId.HasValue)
        {
            Guid participantUserId = filter.ParticipantUserId.Value;

            query = query.Where(activity => activity.Participants.Any(participant =>
                _context.TeamMemberships.Any(membership =>
                    membership.TeamMembershipId == participant.TeamMembershipId
                    && membership.TeamId == teamId
                    && membership.UserId == participantUserId)));
        }

        if (filter.FormerMemberId.HasValue)
        {
            Guid formerMemberId = filter.FormerMemberId.Value;

            query = query.Where(activity => activity.Participants.Any(participant =>
                _context.TeamMemberships.Any(membership =>
                    membership.TeamMembershipId == participant.TeamMembershipId
                    && membership.TeamId == teamId
                    && membership.FormerMemberId == formerMemberId)));
        }

        if (filter.Result.HasValue)
        {
            query = filter.Result.Value switch
            {
                MatchResult.Victory => query.Where(activity => activity.MatchDetail != null
                    && activity.MatchDetail.TeamScore.HasValue
                    && activity.MatchDetail.OpponentScore.HasValue
                    && activity.MatchDetail.TeamScore.Value > activity.MatchDetail.OpponentScore.Value),

                MatchResult.Defeat => query.Where(activity => activity.MatchDetail != null
                    && activity.MatchDetail.TeamScore.HasValue
                    && activity.MatchDetail.OpponentScore.HasValue
                    && activity.MatchDetail.TeamScore.Value < activity.MatchDetail.OpponentScore.Value),

                MatchResult.Draw => query.Where(activity => activity.MatchDetail != null
                    && activity.MatchDetail.TeamScore.HasValue
                    && activity.MatchDetail.OpponentScore.HasValue
                    && activity.MatchDetail.TeamScore.Value == activity.MatchDetail.OpponentScore.Value),

                _ => query
            };
        }

        if (filter.SearchText is not null)
        {
            string normalizedSearchText = filter.SearchText.ToLowerInvariant();

            query = query.Where(activity =>
                activity.Subtitle != null && activity.Subtitle.ToLower().Contains(normalizedSearchText)
                || activity.Description != null && activity.Description.ToLower().Contains(normalizedSearchText)
                || activity.Report != null && activity.Report.ToLower().Contains(normalizedSearchText)
                || activity.MatchDetail != null
                    && activity.MatchDetail.OpponentName != null
                    && activity.MatchDetail.OpponentName.ToLower().Contains(normalizedSearchText));
        }

        return await query
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