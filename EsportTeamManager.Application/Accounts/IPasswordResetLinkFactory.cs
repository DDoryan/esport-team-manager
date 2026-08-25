namespace EsportTeamManager.Application.Accounts;

public interface IPasswordResetLinkFactory
{
    string CreatePasswordResetLink(Guid userId, string token);
}