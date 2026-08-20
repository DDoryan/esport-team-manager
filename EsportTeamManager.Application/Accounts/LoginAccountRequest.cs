namespace EsportTeamManager.Application.Accounts;

public sealed record LoginAccountRequest(string Email, string Password, bool RememberMe);