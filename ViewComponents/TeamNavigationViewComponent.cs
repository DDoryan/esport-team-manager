using System.Security.Claims;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Models.Navigation;
using Microsoft.AspNetCore.Mvc;
using EsportTeamManager.Web.Navigation;

namespace EsportTeamManager.Web.ViewComponents;

public sealed class TeamNavigationViewComponent : ViewComponent
{
    private readonly IUserTeamService _userTeamService;

    public TeamNavigationViewComponent(IUserTeamService userTeamService)
    {
        _userTeamService = userTeamService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Content(string.Empty);
        }

        IReadOnlyCollection<UserTeamSummary> teams = await _userTeamService.GetTeamsForUserAsync(userId.Value, HttpContext.RequestAborted);
        IReadOnlyCollection<TeamNavigationItemViewModel> navigationTeams = teams
            .Select(team => new TeamNavigationItemViewModel(team.TeamId, team.Name, team.Tag))
            .ToArray();
        Guid? requestedTeamId = GetActiveTeamId();
        TeamNavigationItemViewModel? activeTeam = requestedTeamId.HasValue
            ? navigationTeams.SingleOrDefault(team => team.TeamId == requestedTeamId.Value)
            : null;

        if (activeTeam is not null)
        {
            LastVisitedTeamCookie.Write(HttpContext.Response, activeTeam.TeamId);
        }
        else if (!requestedTeamId.HasValue)
        {
            Guid? lastVisitedTeamId = LastVisitedTeamCookie.Read(HttpContext.Request);

            if (lastVisitedTeamId.HasValue)
            {
                activeTeam = navigationTeams.SingleOrDefault(team => team.TeamId == lastVisitedTeamId.Value);

                if (activeTeam is null)
                {
                    LastVisitedTeamCookie.Delete(HttpContext.Response);
                }
            }
        }

        string? currentController = ViewContext.RouteData.Values["controller"]?.ToString();
        string? currentAction = ViewContext.RouteData.Values["action"]?.ToString();
        bool calendarIsActive = activeTeam is not null && string.Equals(currentController, "Activities", StringComparison.OrdinalIgnoreCase);
        bool managementIsActive = activeTeam is not null && string.Equals(currentController, "Teams", StringComparison.OrdinalIgnoreCase) && string.Equals(currentAction, "Management", StringComparison.OrdinalIgnoreCase);
        TeamNavigationViewModel viewModel = new(activeTeam, navigationTeams, calendarIsActive, managementIsActive);

        return View(viewModel);
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }

    private Guid? GetActiveTeamId()
    {
        string? routeTeamId = ViewContext.RouteData.Values["teamId"]?.ToString();
        string? queryTeamId = HttpContext.Request.Query["teamId"].FirstOrDefault();
        string? teamId = string.IsNullOrWhiteSpace(routeTeamId) ? queryTeamId : routeTeamId;

        return Guid.TryParse(teamId, out Guid parsedTeamId) ? parsedTeamId : null;
    }
}