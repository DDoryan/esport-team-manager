namespace EsportTeamManager.Application.Accounts;

public sealed class RegisterAccountRequest
{
    public string Email { get; }

    public string Pseudo { get; }

    public string Tag { get; }

    public string Password { get; }

    public bool MinimumAgeConfirmed { get; }

    public bool TermsAccepted { get; }

    public RegisterAccountRequest(string email, string pseudo, string tag, string password, bool minimumAgeConfirmed, bool termsAccepted)
    {
        Email = email;
        Pseudo = pseudo;
        Tag = tag;
        Password = password;
        MinimumAgeConfirmed = minimumAgeConfirmed;
        TermsAccepted = termsAccepted;
    }
}