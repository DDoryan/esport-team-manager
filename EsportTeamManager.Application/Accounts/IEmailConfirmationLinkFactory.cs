namespace EsportTeamManager.Application.Accounts;

public interface IEmailConfirmationLinkFactory
{
    string CreateEmailConfirmationLink(Guid userId, string token);
}