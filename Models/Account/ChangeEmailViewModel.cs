using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Account;

public sealed class ChangeEmailViewModel
{
    [Required(ErrorMessage = "Le mot de passe actuel est obligatoire.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe actuel")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nouvelle adresse e-mail est obligatoire.")]
    [EmailAddress(ErrorMessage = "La nouvelle adresse e-mail n’est pas valide.")]
    [StringLength(254, ErrorMessage = "La nouvelle adresse e-mail ne peut pas dépasser 254 caractères.")]
    [Display(Name = "Nouvelle adresse e-mail")]
    public string NewEmail { get; set; } = string.Empty;
}