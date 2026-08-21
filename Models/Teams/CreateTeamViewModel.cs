using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Teams;

public sealed class CreateTeamViewModel
{
    [Required(ErrorMessage = "Le nom de l’équipe est obligatoire.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Le nom de l’équipe doit contenir entre 3 et 50 caractères.")]
    [Display(Name = "Nom")]
    public string Name { get; set; } = string.Empty;

    [StringLength(6, MinimumLength = 2, ErrorMessage = "Le tag doit contenir entre 2 et 6 caractères.")]
    [Display(Name = "Tag")]
    public string? Tag { get; set; }

    [Required(ErrorMessage = "Le fuseau horaire est obligatoire.")]
    [StringLength(64, ErrorMessage = "Le fuseau horaire ne peut pas dépasser 64 caractères.")]
    [Display(Name = "Fuseau horaire")]
    public string TimeZoneId { get; set; } = "Europe/Paris";
}