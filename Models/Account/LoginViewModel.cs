using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "L’adresse e-mail est obligatoire.")]
    [EmailAddress(ErrorMessage = "L’adresse e-mail n’est pas valide.")]
    [StringLength(254, ErrorMessage = "L’adresse e-mail ne peut pas dépasser 254 caractères.")]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir entre 8 et 128 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Se souvenir de moi pendant 30 jours")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}