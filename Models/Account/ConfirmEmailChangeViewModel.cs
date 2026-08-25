namespace EsportTeamManager.Web.Models.Account;

public sealed class ConfirmEmailChangeViewModel
{
    public bool Succeeded { get; }

    public string Message { get; }

    public ConfirmEmailChangeViewModel(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }
}