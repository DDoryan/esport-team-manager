namespace EsportTeamManager.Web.Models.Teams;

public sealed class TeamManagementViewModel
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string? Description { get; }

    public string TimeZoneId { get; }

    public bool CurrentUserIsOwner { get; }

    public bool CurrentUserCanInviteMembers { get; }

    public bool CurrentUserCanLeaveTeam { get; }

    public bool HasLogo { get; }

    public IReadOnlyCollection<TeamRoleOptionViewModel> AvailableRoles { get; }

    public PendingOwnershipTransferViewModel? PendingOwnershipTransfer { get; }

    public IReadOnlyCollection<TeamMemberViewModel> Members { get; }

    public UpdateTeamInformationViewModel InformationForm { get; }

    public InviteTeamMemberViewModel InvitationForm { get; }

    public TransferOwnershipViewModel OwnershipTransferForm { get; }

    public TeamManagementViewModel(Guid teamId, string name, string? tag, string? description, string timeZoneId, bool currentUserIsOwner, bool currentUserCanInviteMembers, bool currentUserCanLeaveTeam, IReadOnlyCollection<TeamRoleOptionViewModel> availableRoles, IReadOnlyCollection<TeamMemberViewModel> members, PendingOwnershipTransferViewModel? pendingOwnershipTransfer = null, bool hasLogo = false, InviteTeamMemberViewModel? invitationForm = null, TransferOwnershipViewModel? ownershipTransferForm = null, UpdateTeamInformationViewModel? informationForm = null)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        Description = description;
        TimeZoneId = timeZoneId;
        CurrentUserIsOwner = currentUserIsOwner;
        CurrentUserCanInviteMembers = currentUserCanInviteMembers;
        CurrentUserCanLeaveTeam = currentUserCanLeaveTeam;
        AvailableRoles = availableRoles;
        Members = members;
        PendingOwnershipTransfer = pendingOwnershipTransfer;
        HasLogo = hasLogo;
        InformationForm = informationForm ?? new UpdateTeamInformationViewModel
        {
            TeamId = teamId,
            TeamName = name,
            Name = name,
            Tag = tag,
            Description = description,
            TimeZoneId = timeZoneId,
            HasCurrentLogo = hasLogo,
            AvailableTimeZoneIds = [timeZoneId]
        };
        InvitationForm = invitationForm ?? new InviteTeamMemberViewModel
        {
            TeamId = teamId,
            TeamName = name,
            AvailableRoles = availableRoles
        };
        OwnershipTransferForm = ownershipTransferForm ?? new TransferOwnershipViewModel
        {
            TeamId = teamId,
            TeamName = name,
            AvailableRecipients =
            [
                .. members
                    .Where(member => !member.IsOwner)
                    .Select(member => new OwnershipTransferRecipientOptionViewModel(member.TeamMembershipId, member.Pseudo, member.Tag, member.RoleLabel))
            ]
        };
    }
}