using System.ComponentModel.DataAnnotations;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace RepriseWeb.ViewModels.Strategies;

public class StrategyFormViewModel
{
    public Guid TeamId { get; set; }

    public Guid? StrategyId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public IReadOnlyCollection<StrategyMapOptionViewModel> Maps { get; set; } = Array.Empty<StrategyMapOptionViewModel>();

    [Required(ErrorMessage = "Le nom de la stratégie est obligatoire.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Le nom doit contenir entre 3 et 100 caractères.")]
    [Display(Name = "Nom")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La carte est obligatoire.")]
    [Range(1, int.MaxValue, ErrorMessage = "La carte sélectionnée est invalide.")]
    [Display(Name = "Carte")]
    public int? MapId { get; set; }

    [Required(ErrorMessage = "Le camp est obligatoire.")]
    [Display(Name = "Camp")]
    public StrategySide? Side { get; set; } = StrategySide.Attack;

    [StringLength(5000, ErrorMessage = "La description ne peut pas dépasser 5000 caractères.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(2048, ErrorMessage = "L’URL ne peut pas dépasser 2048 caractères.")]
    [Url(ErrorMessage = "L’URL doit être une adresse HTTP ou HTTPS valide.")]
    [Display(Name = "URL externe")]
    public string? ExternalUrl { get; set; }

    [Display(Name = "Stratégie active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Image")]
    public IFormFile? Image { get; set; }
}