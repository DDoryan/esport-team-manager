namespace EsportTeamManager.Application.Accounts;

public interface IAccountEmailChangeService
{
    Task<ChangeAccountEmailResult> RequestEmailChangeAsync(ChangeAccountEmailRequest request, CancellationToken cancellationToken = default);

    Task<ChangeAccountEmailResult> ConfirmEmailChangeAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}