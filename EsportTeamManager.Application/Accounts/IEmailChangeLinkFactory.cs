namespace EsportTeamManager.Application.Accounts;

public interface IEmailChangeLinkFactory
{
    string CreateEmailChangeLink(Guid userId, string token);
}