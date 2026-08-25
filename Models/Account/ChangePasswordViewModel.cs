using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Account;

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Le mot de passe actuel est obligatoire.")]
    [StringLength(128, ErrorMessage = "Le mot de passe actuel ne peut pas dépasser 128 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe actuel")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nouveau mot de passe est obligatoire.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Le nouveau mot de passe doit contenir entre 8 et 128 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation du nouveau mot de passe est obligatoire.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Les deux nouveaux mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}