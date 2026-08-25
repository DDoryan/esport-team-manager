namespace EsportTeamManager.Application.Accounts;

public interface IAccountProfileService
{
    Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ChangeAccountPasswordResult> ChangePasswordAsync(ChangeAccountPasswordRequest request, CancellationToken cancellationToken = default);
}