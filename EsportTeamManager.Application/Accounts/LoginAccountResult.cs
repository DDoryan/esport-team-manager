namespace EsportTeamManager.Application.Accounts;

public sealed class LoginAccountResult
{
    public bool Succeeded { get; }

    public string ErrorMessage { get; }

    private LoginAccountResult(bool succeeded, string errorMessage)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
    }

    public static LoginAccountResult Success()
    {
        return new LoginAccountResult(true, string.Empty);
    }

    public static LoginAccountResult Failure()
    {
        return new LoginAccountResult(false, "Connexion impossible. Vérifiez vos informations ou réessayez plus tard.");
    }
}