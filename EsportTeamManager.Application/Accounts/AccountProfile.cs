namespace EsportTeamManager.Application.Accounts;

public sealed class AccountProfile
{
    public string Pseudo { get; }

    public string Tag { get; }

    public string Email { get; }

    public AccountProfile(string pseudo, string tag, string email)
    {
        Pseudo = pseudo;
        Tag = tag;
        Email = email;
    }
}