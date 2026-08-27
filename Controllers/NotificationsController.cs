using System.Security.Claims;
using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Application.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Web.Controllers;

[Authorize]
public sealed class NotificationsController : Controller
{
    private readonly IUserNotificationService _userNotificationService;
    private readonly IUserTeamService _userTeamService;

    public NotificationsController(IUserNotificationService userNotificationService, IUserTeamService userTeamService)
    {
        _userNotificationService = userNotificationService;
        _userTeamService = userTeamService;
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        await _userNotificationService.MarkAllAsReadAsync(userId.Value, cancellationToken);

        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> AcceptInvitation(Guid invitationId, string? returnUrl, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        ResolveInvitationRequest request = new(userId.Value, invitationId);
        InvitationActionResult result = await _userNotificationService.AcceptInvitationAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            SetErrorMessage(result.Errors, "L’invitation n’a pas pu être acceptée.");

            return RedirectToReturnUrl(returnUrl);
        }

        TempData["SuccessMessage"] = "L’invitation a été acceptée. Vous avez rejoint l’équipe.";

        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> RefuseInvitation(Guid invitationId, string? returnUrl, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        ResolveInvitationRequest request = new(userId.Value, invitationId);
        InvitationActionResult result = await _userNotificationService.RefuseInvitationAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            SetErrorMessage(result.Errors, "L’invitation n’a pas pu être refusée.");

            return RedirectToReturnUrl(returnUrl);
        }

        TempData["SuccessMessage"] = "L’invitation a été refusée.";

        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> AcceptOwnershipTransfer(Guid teamId, Guid ownershipTransferId, string? returnUrl, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        ResolveOwnershipTransferRequest request = new(userId.Value, teamId, ownershipTransferId);
        OwnershipTransferActionResult result = await _userTeamService.AcceptOwnershipTransferAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            SetErrorMessage(result.Errors, "Le transfert de propriété n’a pas pu être accepté.");

            return RedirectToReturnUrl(returnUrl);
        }

        TempData["SuccessMessage"] = "Le transfert de propriété a été accepté.";

        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> RefuseOwnershipTransfer(Guid teamId, Guid ownershipTransferId, string? returnUrl, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        ResolveOwnershipTransferRequest request = new(userId.Value, teamId, ownershipTransferId);
        OwnershipTransferActionResult result = await _userTeamService.RefuseOwnershipTransferAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            SetErrorMessage(result.Errors, "Le transfert de propriété n’a pas pu être refusé.");

            return RedirectToReturnUrl(returnUrl);
        }

        TempData["SuccessMessage"] = "Le transfert de propriété a été refusé.";

        return RedirectToReturnUrl(returnUrl);
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }

    private IActionResult RedirectToReturnUrl(string? returnUrl)
    {
        if (IsLocalReturnUrl(returnUrl))
        {
            return LocalRedirect(returnUrl!);
        }

        return RedirectToAction("Entry", "Teams");
    }

    private void SetErrorMessage(IReadOnlyCollection<string> errors, string fallbackMessage)
    {
        TempData["ErrorMessage"] = errors.Count == 0 ? fallbackMessage : string.Join(" ", errors);
    }

    private static bool IsLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        if (returnUrl[0] == '/')
        {
            return returnUrl.Length == 1 || returnUrl[1] != '/' && returnUrl[1] != '\\';
        }

        return returnUrl.Length > 1 && returnUrl[0] == '~' && returnUrl[1] == '/';
    }
}