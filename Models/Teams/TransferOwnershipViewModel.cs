using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Teams;

public sealed class TransferOwnershipViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nouveau propriétaire est obligatoire.")]
    [Display(Name = "Nouveau propriétaire")]
    public Guid? RecipientMembershipId { get; set; }

    public IReadOnlyCollection<OwnershipTransferRecipientOptionViewModel> AvailableRecipients { get; set; } = [];
}