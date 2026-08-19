namespace EsportTeamManager.Web.Models.Account;

public sealed class ConfirmEmailViewModel
{
    public bool Succeeded { get; }

    public string Message { get; }

    public ConfirmEmailViewModel(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }
}