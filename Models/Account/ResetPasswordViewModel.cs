using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Account;

public sealed class ResetPasswordViewModel
{
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Le jeton de réinitialisation est obligatoire.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nouveau mot de passe est obligatoire.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir entre 8 et 128 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation du mot de passe est obligatoire.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les deux mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;
}