using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class TeamRole
{
    public int TeamRoleId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public Guid? TeamId { get; private set; }

    private TeamRole()
    {
    }

    public TeamRole(int teamRoleId, string code, string label, bool isSystem, Guid? teamId = null)
    {
        if (teamRoleId <= 0)
        {
            throw new DomainException("The team role identifier must be positive.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 30)
        {
            throw new DomainException("The team role code must contain between 1 and 30 characters.");
        }

        if (string.IsNullOrWhiteSpace(label) || label.Length > 50)
        {
            throw new DomainException("The team role label must contain between 1 and 50 characters.");
        }

        if (isSystem && teamId.HasValue)
        {
            throw new DomainException("A system team role cannot belong to a team.");
        }

        if (!isSystem && !teamId.HasValue)
        {
            throw new DomainException("A custom team role must belong to a team.");
        }

        TeamRoleId = teamRoleId;
        Code = code.Trim();
        Label = label.Trim();
        IsSystem = isSystem;
        TeamId = teamId;
    }
}