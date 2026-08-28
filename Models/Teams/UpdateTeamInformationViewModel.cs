using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EsportTeamManager.Web.Models.Teams;

public sealed class UpdateTeamInformationViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom de l’équipe est obligatoire.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Le nom de l’équipe doit contenir entre 3 et 50 caractères.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(6, MinimumLength = 2, ErrorMessage = "Le tag de l’équipe doit contenir entre 2 et 6 caractères.")]
    public string? Tag { get; set; }

    [StringLength(500, ErrorMessage = "La description de l’équipe ne peut pas dépasser 500 caractères.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Le fuseau horaire est obligatoire.")]
    [StringLength(64, ErrorMessage = "Le fuseau horaire sélectionné n’est pas valide.")]
    public string TimeZoneId { get; set; } = "Europe/Paris";

    [Display(Name = "Nouveau logo")]
    public IFormFile? Logo { get; set; }

    public bool HasCurrentLogo { get; set; }

    public IReadOnlyCollection<string> AvailableTimeZoneIds { get; set; } = [];
}