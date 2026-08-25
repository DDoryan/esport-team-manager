namespace EsportTeamManager.Web.Models.Account;

public sealed class ProfileViewModel
{
    public string Pseudo { get; }

    public string Tag { get; }

    public string Email { get; }

    public string DisplayIdentity => $"{Pseudo} #{Tag}";

    public ProfileViewModel(string pseudo, string tag, string email)
    {
        Pseudo = pseudo;
        Tag = tag;
        Email = email;
    }
}