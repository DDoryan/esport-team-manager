using System.ComponentModel.DataAnnotations;

namespace RepriseWeb.ViewModels.Activities;

public sealed class EditActivityViewModel
{
    public Guid TeamId { get; set; }

    public Guid ActivityId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;

    public string TypeCode { get; set; } = string.Empty;

    public string TypeLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le type d’activité est obligatoire.")]
    [Display(Name = "Type")]
    public int? ActivityTypeId { get; set; }

    [StringLength(100, ErrorMessage = "Le sous-titre ne peut pas dépasser 100 caractères.")]
    [Display(Name = "Sous-titre")]
    public string? Subtitle { get; set; }

    [Required(ErrorMessage = "Le début prévu est obligatoire.")]
    [Display(Name = "Début prévu")]
    public DateTime? PlannedStartLocal { get; set; }

    [Required(ErrorMessage = "La fin prévue est obligatoire.")]
    [Display(Name = "Fin prévue")]
    public DateTime? PlannedEndLocal { get; set; }

    [StringLength(2000, ErrorMessage = "La description ne peut pas dépasser 2 000 caractères.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(5000, ErrorMessage = "Le compte rendu ne peut pas dépasser 5 000 caractères.")]
    [Display(Name = "Compte rendu")]
    public string? Report { get; set; }

    public string StatusLabel { get; set; } = string.Empty;

    public string? CancellationReason { get; set; }

    public string? OpponentName { get; set; }

    public int? TeamScore { get; set; }

    public int? OpponentScore { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool CanEdit { get; set; }

    public IReadOnlyCollection<ActivityTypeOptionViewModel> ActivityTypes { get; set; } = Array.Empty<ActivityTypeOptionViewModel>();

    public IReadOnlyCollection<ActivityEditParticipantViewModel> Participants { get; set; } = Array.Empty<ActivityEditParticipantViewModel>();

    public List<ActivityEditLinkViewModel> Links { get; set; } = [];
}