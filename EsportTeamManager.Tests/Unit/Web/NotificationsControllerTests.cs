using System.Security.Claims;
using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class NotificationsControllerTests
{
    [Fact]
    public async Task MarkAllAsRead_WhenUserIsAuthenticated_MarksNotificationsAndReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        StubUserNotificationService notificationService = new();
        NotificationsController controller = CreateController(notificationService, new StubUserTeamService(), userId);

        IActionResult result = await controller.MarkAllAsRead(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(userId, notificationService.LastMarkedAsReadUserId);
    }

    [Fact]
    public async Task AcceptInvitation_WhenServiceSucceeds_RedirectsToLocalReturnUrl()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        string returnUrl = $"/Activities/Index?teamId={teamId}";
        StubUserNotificationService notificationService = new(acceptResult: InvitationActionResult.Success(teamId));
        NotificationsController controller = CreateController(notificationService, new StubUserTeamService(), userId);

        IActionResult result = await controller.AcceptInvitation(invitationId, returnUrl, CancellationToken.None);

        LocalRedirectResult redirectResult = Assert.IsType<LocalRedirectResult>(result);

        Assert.Equal(returnUrl, redirectResult.Url);
        Assert.NotNull(notificationService.LastAcceptedInvitationRequest);
        Assert.Equal("L’invitation a été acceptée. Vous avez rejoint l’équipe.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task RefuseInvitation_WhenServiceSucceeds_RedirectsToLocalReturnUrl()
    {
        Guid userId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        const string returnUrl = "/Teams/Index";
        StubUserNotificationService notificationService = new(refuseResult: InvitationActionResult.Success(Guid.NewGuid()));
        NotificationsController controller = CreateController(notificationService, new StubUserTeamService(), userId);

        IActionResult result = await controller.RefuseInvitation(invitationId, returnUrl, CancellationToken.None);

        LocalRedirectResult redirectResult = Assert.IsType<LocalRedirectResult>(result);

        Assert.Equal(returnUrl, redirectResult.Url);
        Assert.NotNull(notificationService.LastRefusedInvitationRequest);
        Assert.Equal("L’invitation a été refusée.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task AcceptOwnershipTransfer_WhenServiceSucceeds_RedirectsToLocalReturnUrl()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownershipTransferId = Guid.NewGuid();
        string returnUrl = $"/Teams/Management?teamId={teamId}";
        StubUserTeamService teamService = new(acceptResult: OwnershipTransferActionResult.Success(ownershipTransferId));
        NotificationsController controller = CreateController(new StubUserNotificationService(), teamService, userId);

        IActionResult result = await controller.AcceptOwnershipTransfer(teamId, ownershipTransferId, returnUrl, CancellationToken.None);

        LocalRedirectResult redirectResult = Assert.IsType<LocalRedirectResult>(result);

        Assert.Equal(returnUrl, redirectResult.Url);
        Assert.NotNull(teamService.LastAcceptedOwnershipTransferRequest);
        Assert.Equal("Le transfert de propriété a été accepté.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task RefuseOwnershipTransfer_WhenServiceSucceeds_RedirectsToLocalReturnUrl()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownershipTransferId = Guid.NewGuid();
        string returnUrl = $"/Activities/Index?teamId={teamId}";
        StubUserTeamService teamService = new(refuseResult: OwnershipTransferActionResult.Success(ownershipTransferId));
        NotificationsController controller = CreateController(new StubUserNotificationService(), teamService, userId);

        IActionResult result = await controller.RefuseOwnershipTransfer(teamId, ownershipTransferId, returnUrl, CancellationToken.None);

        LocalRedirectResult redirectResult = Assert.IsType<LocalRedirectResult>(result);

        Assert.Equal(returnUrl, redirectResult.Url);
        Assert.NotNull(teamService.LastRefusedOwnershipTransferRequest);
        Assert.Equal("Le transfert de propriété a été refusé.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task AcceptInvitation_WhenReturnUrlIsExternal_RedirectsToTeamEntry()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubUserNotificationService notificationService = new(acceptResult: InvitationActionResult.Success(teamId));
        NotificationsController controller = CreateController(notificationService, new StubUserTeamService(), userId);

        IActionResult result = await controller.AcceptInvitation(Guid.NewGuid(), "https://example.test/external", CancellationToken.None);

        RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal("Entry", redirectResult.ActionName);
        Assert.Equal("Teams", redirectResult.ControllerName);
    }

    [Fact]
    public async Task AcceptOwnershipTransfer_WhenAccessIsDenied_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        StubUserTeamService teamService = new(acceptResult: OwnershipTransferActionResult.Denied());
        NotificationsController controller = CreateController(new StubUserNotificationService(), teamService, userId);

        IActionResult result = await controller.AcceptOwnershipTransfer(Guid.NewGuid(), Guid.NewGuid(), "/Teams/Index", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.NotNull(teamService.LastAcceptedOwnershipTransferRequest);
    }

    private static NotificationsController CreateController(IUserNotificationService notificationService, IUserTeamService teamService, Guid userId)
    {
        Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, userId.ToString())];
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        NotificationsController controller = new(notificationService, teamService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider())
        };

        return controller;
    }

    private sealed class StubUserNotificationService : IUserNotificationService
    {
        private readonly InvitationActionResult _acceptResult;
        private readonly InvitationActionResult _refuseResult;

        public Guid? LastMarkedAsReadUserId { get; private set; }

        public ResolveInvitationRequest? LastAcceptedInvitationRequest { get; private set; }

        public ResolveInvitationRequest? LastRefusedInvitationRequest { get; private set; }

        public StubUserNotificationService(InvitationActionResult? acceptResult = null, InvitationActionResult? refuseResult = null)
        {
            _acceptResult = acceptResult ?? InvitationActionResult.Denied();
            _refuseResult = refuseResult ?? InvitationActionResult.Denied();
        }

        public Task<InvitationActionResult> AcceptInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastAcceptedInvitationRequest = request;

            return Task.FromResult(_acceptResult);
        }

        public Task<IReadOnlyCollection<UserNotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<UserNotificationSummary>>([]);
        }

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastMarkedAsReadUserId = userId;

            return Task.CompletedTask;
        }

        public Task<InvitationActionResult> RefuseInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRefusedInvitationRequest = request;

            return Task.FromResult(_refuseResult);
        }
    }

    private sealed class StubUserTeamService : IUserTeamService
    {
        private readonly OwnershipTransferActionResult _acceptResult;
        private readonly OwnershipTransferActionResult _refuseResult;

        public ResolveOwnershipTransferRequest? LastAcceptedOwnershipTransferRequest { get; private set; }

        public ResolveOwnershipTransferRequest? LastRefusedOwnershipTransferRequest { get; private set; }

        public StubUserTeamService(OwnershipTransferActionResult? acceptResult = null, OwnershipTransferActionResult? refuseResult = null)
        {
            _acceptResult = acceptResult ?? OwnershipTransferActionResult.Denied();
            _refuseResult = refuseResult ?? OwnershipTransferActionResult.Denied();
        }

        public Task<OwnershipTransferActionResult> AcceptOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastAcceptedOwnershipTransferRequest = request;

            return Task.FromResult(_acceptResult);
        }

        public Task<OwnershipTransferActionResult> CancelOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TeamMembershipActionResult.Denied());
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
            return Task.FromResult<IReadOnlyCollection<UserTeamSummary>>([]);
        }

        public Task<OwnershipTransferActionResult> InitiateOwnershipTransferAsync(InitiateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InviteTeamMemberResult.Denied());
        }

        public Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TeamMembershipActionResult.Denied());
        }

        public Task<OwnershipTransferActionResult> RefuseOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRefusedOwnershipTransferRequest = request;

            return Task.FromResult(_refuseResult);
        }

        public Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TeamMembershipActionResult.Denied());
        }
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object> _values = [];

        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return _values;
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}