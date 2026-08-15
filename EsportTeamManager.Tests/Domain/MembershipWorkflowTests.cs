using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class MembershipWorkflowTests
    {
        [Fact]
        public void TeamMembership_Constructor_CreatesActiveMembership()
        {
            Guid membershipId = Guid.NewGuid();
            Guid teamId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;

            TeamMembership membership = new TeamMembership(membershipId, teamId, userId, 1, joinedAtUtc);

            Assert.Equal(membershipId, membership.TeamMembershipId);
            Assert.Equal(teamId, membership.TeamId);
            Assert.Equal(userId, membership.UserId);
            Assert.Null(membership.FormerMemberId);
            Assert.Equal(MembershipStatus.Active, membership.Status);
            Assert.Null(membership.LeftAtUtc);
        }

        [Fact]
        public void TeamMembership_ChangeRole_UpdatesRole()
        {
            TeamMembership membership = new TeamMembership(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);

            membership.ChangeRole(2);

            Assert.Equal(2, membership.TeamRoleId);
        }

        [Fact]
        public void TeamMembership_Leave_ClosesMembership()
        {
            DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset leftAtUtc = joinedAtUtc.AddDays(10);
            TeamMembership membership = new TeamMembership(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, joinedAtUtc);

            membership.Leave(leftAtUtc);

            Assert.Equal(MembershipStatus.Left, membership.Status);
            Assert.Equal(leftAtUtc, membership.LeftAtUtc);
        }

        [Fact]
        public void TeamMembership_Pseudonymize_ReplacesUserWithFormerMember()
        {
            Guid userId = Guid.NewGuid();
            Guid formerMemberId = Guid.NewGuid();
            DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;
            TeamMembership membership = new TeamMembership(Guid.NewGuid(), Guid.NewGuid(), userId, 1, joinedAtUtc);

            membership.Pseudonymize(formerMemberId, joinedAtUtc.AddDays(1));

            Assert.Null(membership.UserId);
            Assert.Equal(formerMemberId, membership.FormerMemberId);
            Assert.Equal(MembershipStatus.Removed, membership.Status);
            Assert.NotNull(membership.LeftAtUtc);
        }

        [Fact]
        public void Invitation_Accept_CreatesAcceptedResult()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Guid createdMembershipId = Guid.NewGuid();
            Invitation invitation = new Invitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, createdAtUtc);

            invitation.Accept(createdMembershipId, createdAtUtc.AddMinutes(5));

            Assert.Equal(RequestStatus.Accepted, invitation.Status);
            Assert.Equal(createdMembershipId, invitation.CreatedMembershipId);
            Assert.Equal(createdAtUtc.AddMinutes(5), invitation.ResolvedAtUtc);
        }

        [Fact]
        public void Invitation_ResolveTwice_ThrowsDomainException()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Invitation invitation = new Invitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, createdAtUtc);
            invitation.Refuse(createdAtUtc.AddMinutes(5));

            DomainException exception = Assert.Throws<DomainException>(() => invitation.Cancel(createdAtUtc.AddMinutes(10)));

            Assert.Equal("Only a pending invitation can be resolved.", exception.Message);
        }

        [Fact]
        public void OwnershipTransfer_Accept_ChangesStatus()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            OwnershipTransfer transfer = new OwnershipTransfer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), createdAtUtc);

            transfer.Accept(createdAtUtc.AddMinutes(5));

            Assert.Equal(RequestStatus.Accepted, transfer.Status);
            Assert.Equal(createdAtUtc.AddMinutes(5), transfer.ResolvedAtUtc);
        }

        [Fact]
        public void OwnershipTransfer_WithSameInitiatorAndRecipient_ThrowsDomainException()
        {
            Guid membershipId = Guid.NewGuid();

            DomainException exception = Assert.Throws<DomainException>(() => new OwnershipTransfer(Guid.NewGuid(), Guid.NewGuid(), membershipId, membershipId, DateTimeOffset.UtcNow));

            Assert.Equal("The initiator and recipient memberships must be different.", exception.Message);
        }
    }
}