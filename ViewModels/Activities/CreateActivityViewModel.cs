using System.ComponentModel.DataAnnotations;

namespace RepriseWeb.ViewModels.Activities;

public sealed class CreateActivityViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;

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

    [StringLength(100, ErrorMessage = "Le nom de l’équipe adverse ne peut pas dépasser 100 caractères.")]
    [Display(Name = "Équipe adverse")]
    public string? OpponentName { get; set; }

    public List<CreateActivityLinkViewModel> Links { get; set; } = [];

    public List<Guid> ParticipantMembershipIds { get; set; } = [];

    public IReadOnlyCollection<ActivityTypeOptionViewModel> ActivityTypes { get; set; } = Array.Empty<ActivityTypeOptionViewModel>();

    public IReadOnlyCollection<ActivityParticipantOptionViewModel> Participants { get; set; } = Array.Empty<ActivityParticipantOptionViewModel>();
}