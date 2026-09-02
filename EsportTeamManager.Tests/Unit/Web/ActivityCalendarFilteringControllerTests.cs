using System.Security.Claims;
using EsportTeamManager.Application.Activities;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Controllers;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class ActivityCalendarFilteringControllerTests
{
    [Fact]
    public async Task Events_WhenCombinedFiltersAreValid_ForwardsAllCriteria()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid participantUserId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId);
        DateTimeOffset start = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = start.AddDays(28);

        IActionResult result = await controller.Events(
            teamId,
            start,
            end,
            "Pracc,Meeting",
            "Completed",
            $"user:{participantUserId:D}",
            MatchResult.Victory,
            "  Navi  ",
            CancellationToken.None);

        Assert.IsType<JsonResult>(result);

        ActivityCalendarFilter filter = Assert.IsType<ActivityCalendarFilter>(calendarService.LastFilter);

        Assert.Equal(2, filter.TypeCodes.Count);
        Assert.Contains("Pracc", filter.TypeCodes);
        Assert.Contains("Meeting", filter.TypeCodes);
        Assert.Single(filter.Statuses);
        Assert.Contains(ActivityStatus.Completed, filter.Statuses);
        Assert.Equal(participantUserId, filter.ParticipantUserId);
        Assert.Null(filter.FormerMemberId);
        Assert.Equal(MatchResult.Victory, filter.Result);
        Assert.Equal("Navi", filter.SearchText);
    }

    [Fact]
    public async Task Events_WhenFormerMemberFilterIsValid_ForwardsFormerMemberIdentity()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid formerMemberId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId);
        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset end = start.AddDays(7);

        IActionResult result = await controller.Events(
            teamId,
            start,
            end,
            participant: $"former:{formerMemberId:D}",
            cancellationToken: CancellationToken.None);

        Assert.IsType<JsonResult>(result);

        ActivityCalendarFilter filter = Assert.IsType<ActivityCalendarFilter>(calendarService.LastFilter);

        Assert.Null(filter.ParticipantUserId);
        Assert.Equal(formerMemberId, filter.FormerMemberId);
    }

    [Fact]
    public async Task Events_WhenParticipantFilterIsInvalid_ReturnsBadRequest()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId);
        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset end = start.AddDays(7);

        IActionResult result = await controller.Events(
            teamId,
            start,
            end,
            participant: "membership:invalid",
            cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(calendarService.LastFilter);
    }

    [Fact]
    public async Task Events_WhenSearchTextExceedsMaximumLength_ReturnsBadRequest()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId);
        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset end = start.AddDays(7);

        IActionResult result = await controller.Events(
            teamId,
            start,
            end,
            searchText: new string('a', 101),
            cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(calendarService.LastFilter);
    }

    private static ActivitiesController CreateController(StubActivityCalendarService calendarService, StubUserTeamService userTeamService, Guid userId)
    {
        ClaimsIdentity identity = new(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "Test");

        ActivitiesController controller = new(calendarService, null!, null!, userTeamService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return controller;
    }

    private sealed class StubActivityCalendarService : IActivityCalendarService
    {
        public ActivityCalendarFilter? LastFilter { get; private set; }

        public Task<IReadOnlyCollection<ActivityCalendarParticipantOption>> GetParticipantOptionsAsync(Guid teamId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<ActivityCalendarParticipantOption>>([]);
        }

        public Task<IReadOnlyCollection<CalendarActivitySummary>> GetForPeriodAsync(Guid teamId, DateTimeOffset periodStart, DateTimeOffset periodEnd, ActivityCalendarFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;

            return Task.FromResult<IReadOnlyCollection<CalendarActivitySummary>>([]);
        }
    }

    private sealed class StubUserTeamService : IUserTeamService
    {
        private readonly IReadOnlyCollection<UserTeamSummary> _teams;

        public StubUserTeamService(IReadOnlyCollection<UserTeamSummary> teams)
        {
            _teams = teams;
        }

        public Task<OwnershipTransferActionResult> AcceptOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OwnershipTransferActionResult> CancelOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams);
        }

        public Task<OwnershipTransferActionResult> InitiateOwnershipTransferAsync(InitiateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OwnershipTransferActionResult> RefuseOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UpdateTeamInformationResult> UpdateInformationAsync(UpdateTeamInformationRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DeleteTeamResult> DeleteTeamAsync(DeleteTeamRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(DeleteTeamResult.Denied());
        }
    }
}
