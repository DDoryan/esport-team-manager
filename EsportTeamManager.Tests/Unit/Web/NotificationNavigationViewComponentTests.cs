using System.Security.Claims;
using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Web.Models.Notifications;
using EsportTeamManager.Web.ViewComponents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class NotificationNavigationViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_WhenUserHasPendingInvitation_ReturnsMappedNotification()
    {
        Guid userId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        UserNotificationSummary notification = new(notificationId, UserNotificationKind.Invitation, invitationId, teamId, "Phoenix Academy", "PHX", "LoginTest", "A03", "Joueur", RequestStatus.Pending, createdAtUtc, null);
        StubUserNotificationService service = new(2, [notification]);
        NotificationNavigationViewComponent component = CreateComponent(service, userId);

        IViewComponentResult result = await component.InvokeAsync();

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        NotificationNavigationViewModel viewModel = Assert.IsType<NotificationNavigationViewModel>(viewResult.ViewData?.Model);
        NotificationItemViewModel item = Assert.Single(viewModel.Notifications);

        Assert.Equal(2, viewModel.UnreadCount);
        Assert.Equal(notificationId, item.NotificationId);
        Assert.Equal(UserNotificationKind.Invitation, item.Kind);
        Assert.Equal(invitationId, item.SourceId);
        Assert.Equal(teamId, item.TeamId);
        Assert.Equal("Phoenix Academy", item.TeamName);
        Assert.Equal("PHX", item.TeamTag);
        Assert.Equal("LoginTest", item.ActorPseudo);
        Assert.Equal("A03", item.ActorTag);
        Assert.Equal("Joueur", item.ProposedRoleLabel);
        Assert.Equal("En attente", item.StatusLabel);
        Assert.True(item.IsPending);
        Assert.Equal(createdAtUtc, item.CreatedAtUtc);
        Assert.Equal(userId, service.LastUnreadCountUserId);
        Assert.Equal(userId, service.LastGetForUserId);
    }

    [Fact]
    public async Task InvokeAsync_WhenTransferIsCancelled_ReturnsResolvedNotification()
    {
        Guid userId = Guid.NewGuid();
        UserNotificationSummary notification = new(Guid.NewGuid(), UserNotificationKind.OwnershipTransfer, Guid.NewGuid(), Guid.NewGuid(), "Phoenix Academy", "PHX", "LoginTest", "A03", null, RequestStatus.Cancelled, DateTimeOffset.UtcNow, null);
        StubUserNotificationService service = new(0, [notification]);
        NotificationNavigationViewComponent component = CreateComponent(service, userId);

        IViewComponentResult result = await component.InvokeAsync();

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        NotificationNavigationViewModel viewModel = Assert.IsType<NotificationNavigationViewModel>(viewResult.ViewData?.Model);
        NotificationItemViewModel item = Assert.Single(viewModel.Notifications);

        Assert.Equal(0, viewModel.UnreadCount);
        Assert.Equal(UserNotificationKind.OwnershipTransfer, item.Kind);
        Assert.Equal("Annulée", item.StatusLabel);
        Assert.False(item.IsPending);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserIdentifierIsMissing_ReturnsEmptyContent()
    {
        StubUserNotificationService service = new(2, []);
        NotificationNavigationViewComponent component = CreateComponent(service, null);

        IViewComponentResult result = await component.InvokeAsync();

        ContentViewComponentResult contentResult = Assert.IsType<ContentViewComponentResult>(result);

        Assert.Equal(string.Empty, contentResult.Content);
        Assert.Null(service.LastUnreadCountUserId);
        Assert.Null(service.LastGetForUserId);
    }

    private static NotificationNavigationViewComponent CreateComponent(IUserNotificationService service, Guid? userId)
    {
        DefaultHttpContext httpContext = new();

        if (userId.HasValue)
        {
            Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())];
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        ViewDataDictionary viewData = new(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        ViewContext viewContext = new()
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ViewData = viewData
        };
        NotificationNavigationViewComponent component = new(service)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = viewContext
            }
        };

        return component;
    }

    private sealed class StubUserNotificationService : IUserNotificationService
    {
        private readonly int _unreadCount;
        private readonly IReadOnlyCollection<UserNotificationSummary> _notifications;

        public Guid? LastUnreadCountUserId { get; private set; }

        public Guid? LastGetForUserId { get; private set; }

        public StubUserNotificationService(int unreadCount, IReadOnlyCollection<UserNotificationSummary> notifications)
        {
            _unreadCount = unreadCount;
            _notifications = notifications;
        }

        public Task<InvitationActionResult> AcceptInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InvitationActionResult.Denied());
        }

        public Task<IReadOnlyCollection<UserNotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastGetForUserId = userId;

            return Task.FromResult(_notifications);
        }

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastUnreadCountUserId = userId;

            return Task.FromResult(_unreadCount);
        }

        public Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<InvitationActionResult> RefuseInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InvitationActionResult.Denied());
        }
    }
}