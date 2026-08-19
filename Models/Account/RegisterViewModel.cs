using System.ComponentModel.DataAnnotations;
using EsportTeamManager.Web.Models.Validation;

namespace EsportTeamManager.Web.Models.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "L’adresse e-mail est obligatoire.")]
    [EmailAddress(ErrorMessage = "L’adresse e-mail n’est pas valide.")]
    [StringLength(254, ErrorMessage = "L’adresse e-mail ne peut pas dépasser 254 caractères.")]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le pseudonyme est obligatoire.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Le pseudonyme doit contenir entre 3 et 20 caractères.")]
    [Display(Name = "Pseudonyme")]
    public string Pseudo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le tag est obligatoire.")]
    [StringLength(5, MinimumLength = 3, ErrorMessage = "Le tag doit contenir entre 3 et 5 caractères.")]
    [RegularExpression("^[a-zA-Z0-9]+$", ErrorMessage = "Le tag ne peut contenir que des lettres et des chiffres.")]
    [Display(Name = "Tag")]
    public string Tag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation du mot de passe est obligatoire.")]
    [Compare(nameof(Password), ErrorMessage = "Les deux mots de passe ne correspondent pas.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmation du mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MustBeTrue(ErrorMessage = "Vous devez attester avoir au moins 15 ans.")]
    [Display(Name = "J’atteste avoir au moins 15 ans")]
    public bool MinimumAgeConfirmed { get; set; }

    [MustBeTrue(ErrorMessage = "Vous devez accepter les conditions générales d’utilisation.")]
    [Display(Name = "J’accepte les conditions générales d’utilisation")]
    public bool TermsAccepted { get; set; }
}