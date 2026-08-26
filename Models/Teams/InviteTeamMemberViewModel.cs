using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Teams;

public sealed class InviteTeamMemberViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L’identité du membre est obligatoire.")]
    [StringLength(26, MinimumLength = 7, ErrorMessage = "L’identité doit être saisie sous la forme Pseudo#Tag.")]
    [Display(Name = "Pseudo#tag")]
    public string RecipientIdentity { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Le rôle proposé est obligatoire.")]
    [Display(Name = "Rôle proposé")]
    public int ProposedTeamRoleId { get; set; }

    public IReadOnlyCollection<TeamRoleOptionViewModel> AvailableRoles { get; set; } = [];
}