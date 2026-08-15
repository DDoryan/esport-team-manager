using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class TeamMembership
{
    public Guid TeamMembershipId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? FormerMemberId { get; private set; }

    public int TeamRoleId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; private set; }

    public DateTimeOffset? LeftAtUtc { get; private set; }

    private TeamMembership()
    {
    }

    public TeamMembership(Guid teamMembershipId, Guid teamId, Guid userId, int teamRoleId, DateTimeOffset joinedAtUtc)
    {
        if (teamMembershipId == Guid.Empty)
        {
            throw new DomainException( "The membership identifier cannot be empty.");
        }

        if (teamId == Guid.Empty)
        {
            throw new DomainException("The team identifier cannot be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("The user identifier cannot be empty.");
        }

        if (teamRoleId <= 0)
        {
            throw new DomainException("The team role identifier must be positive.");
        }

        TeamMembershipId = teamMembershipId;
        TeamId = teamId;
        UserId = userId;
        FormerMemberId = null;
        TeamRoleId = teamRoleId;
        Status = MembershipStatus.Active;
        JoinedAtUtc = joinedAtUtc.ToUniversalTime();
        LeftAtUtc = null;
    }

    public void ChangeRole(int newTeamRoleId)
    {
        EnsureActive();

        if (newTeamRoleId <= 0)
        {
            throw new DomainException("The team role identifier must be positive.");
        }

        TeamRoleId = newTeamRoleId;
    }

    public void Leave(DateTimeOffset leftAtUtc)
    {
        CloseMembership(MembershipStatus.Left, leftAtUtc);
    }

    public void Remove(DateTimeOffset removedAtUtc)
    {
        CloseMembership(MembershipStatus.Removed, removedAtUtc);
    }

    public void Pseudonymize(Guid formerMemberId, DateTimeOffset removedAtUtc)
    {
        if (formerMemberId == Guid.Empty)
        {
            throw new DomainException("The former member identifier cannot be empty.");
        }

        if (!UserId.HasValue || FormerMemberId.HasValue)
        {
            throw new DomainException("The membership has already been pseudonymized.");
        }

        UserId = null;
        FormerMemberId = formerMemberId;

        if (Status == MembershipStatus.Active)
        {
            DateTimeOffset normalizedDate = removedAtUtc.ToUniversalTime();

            if (normalizedDate < JoinedAtUtc)
            {
                throw new DomainException("The removal date cannot precede the joining date.");
            }

            Status = MembershipStatus.Removed;
            LeftAtUtc = normalizedDate;
        }
    }

    private void CloseMembership(MembershipStatus finalStatus, DateTimeOffset leftAtUtc)
    {
        EnsureActive();

        DateTimeOffset normalizedDate = leftAtUtc.ToUniversalTime();

        if (normalizedDate < JoinedAtUtc)
        {
            throw new DomainException("The leaving date cannot precede the joining date.");
        }

        Status = finalStatus;
        LeftAtUtc = normalizedDate;
    }

    private void EnsureActive()
    {
        if (Status != MembershipStatus.Active)
        {
            throw new DomainException("Only an active membership can be modified.");
        }
    }
}