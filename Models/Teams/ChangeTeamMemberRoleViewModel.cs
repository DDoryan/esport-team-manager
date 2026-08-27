using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Teams;

public sealed class ChangeTeamMemberRoleViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public Guid TeamMembershipId { get; set; }

    public string MemberIdentity { get; set; } = string.Empty;

    [Display(Name = "Nouveau rôle")]
    [Range(1, int.MaxValue, ErrorMessage = "Le rôle est obligatoire.")]
    public int NewTeamRoleId { get; set; }

    public IReadOnlyCollection<TeamRoleOptionViewModel> AvailableRoles { get; set; } = Array.Empty<TeamRoleOptionViewModel>();
}