using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Teams;

public sealed class DeleteTeamViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    [Display(Name = "Saisir le nom de l’équipe")]
    [Required(ErrorMessage = "Le nom de l’équipe est obligatoire.")]
    [StringLength(50, ErrorMessage = "Le nom de l’équipe ne peut pas dépasser 50 caractères.")]
    public string ConfirmationName { get; set; } = string.Empty;
}