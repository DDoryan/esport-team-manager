using System.Security.Claims;
using EsportTeamManager.Application.Activities;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class ActivitiesControllerTests
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

    [Fact]
    public async Task Delete_WhenDeletionSucceeds_RedirectsToCalendarWithSuccessMessage()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid activityId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StubActivityEditingService activityEditingService = new()
        {
            DeleteResult = DeleteActivityResult.Success(2, 1, 1)
        };
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId, activityEditingService);

        IActionResult result = await controller.Delete(teamId, activityId, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(ActivitiesController.Index), redirect.ActionName);
        Assert.Equal(teamId, redirect.RouteValues!["teamId"]);

        DeleteActivityRequest request = Assert.IsType<DeleteActivityRequest>(activityEditingService.LastDeleteRequest);

        Assert.Equal(userId, request.ActorUserId);
        Assert.Equal(teamId, request.TeamId);
        Assert.Equal(activityId, request.ActivityId);

        string successMessage = Assert.IsType<string>(controller.TempData["SuccessMessage"]);

        Assert.Contains("2 participants", successMessage);
        Assert.Contains("1 lien", successMessage);
        Assert.Contains("1 association de stratégie", successMessage);
        Assert.Contains("Les stratégies ont été conservées.", successMessage);
    }

    [Fact]
    public async Task Delete_WhenDeletionFails_RedirectsToEditWithErrorMessage()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid activityId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StubActivityEditingService activityEditingService = new()
        {
            DeleteResult = DeleteActivityResult.Failure(["La suppression a échoué."])
        };
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId, activityEditingService);

        IActionResult result = await controller.Delete(teamId, activityId, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(ActivitiesController.Edit), redirect.ActionName);
        Assert.Equal(teamId, redirect.RouteValues!["teamId"]);
        Assert.Equal(activityId, redirect.RouteValues["activityId"]);
        Assert.Equal("La suppression a échoué.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Delete_WhenUserDoesNotBelongToTeam_ReturnsForbidWithoutCallingService()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid activityId = Guid.NewGuid();
        StubActivityCalendarService calendarService = new();
        StubUserTeamService userTeamService = new([]);
        StubActivityEditingService activityEditingService = new();
        ActivitiesController controller = CreateController(calendarService, userTeamService, userId, activityEditingService);

        IActionResult result = await controller.Delete(teamId, activityId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(activityEditingService.LastDeleteRequest);
    }

    private static ActivitiesController CreateController(StubActivityCalendarService calendarService, StubUserTeamService userTeamService, Guid userId, StubActivityEditingService? activityEditingService = null)
    {
        ClaimsIdentity identity = new(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "Test");

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(identity)
        };

        ActivitiesController controller = new(calendarService, null!, activityEditingService ?? new StubActivityEditingService(), userTeamService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider())
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

    private sealed class StubActivityEditingService : IActivityEditingService
    {
        public DeleteActivityResult DeleteResult { get; set; } = DeleteActivityResult.Success(0, 0, 0);

        public DeleteActivityRequest? LastDeleteRequest { get; private set; }

        public Task<ActivityEditDetails?> GetAsync(Guid userId, Guid teamId, Guid activityId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UpdateActivityResult> UpdateAsync(UpdateActivityRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DeleteActivityResult> DeleteAsync(DeleteActivityRequest request, CancellationToken cancellationToken = default)
        {
            LastDeleteRequest = request;

            return Task.FromResult(DeleteResult);
        }
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
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