namespace EsportTeamManager.Application.Accounts;

public interface IAccountPasswordResetService
{
    Task<AccountPasswordResetResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task<AccountPasswordResetResult> ResetPasswordAsync(Guid userId, string token, string newPassword);
}