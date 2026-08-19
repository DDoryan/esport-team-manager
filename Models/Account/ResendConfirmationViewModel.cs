using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Account;

public sealed class ResendConfirmationViewModel
{
    [Required(ErrorMessage = "L’adresse e-mail est obligatoire.")]
    [EmailAddress(ErrorMessage = "L’adresse e-mail n’est pas valide.")]
    [StringLength(254, ErrorMessage = "L’adresse e-mail ne peut pas dépasser 254 caractères.")]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;
}