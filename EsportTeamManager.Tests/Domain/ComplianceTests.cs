using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class ComplianceTests
    {
        [Fact]
        public void ReservedIdentity_WithValidHash_CreatesUppercaseReservation()
        {
            string identityHash = new string('a', 64);
            DateTimeOffset reservedAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(2));

            ReservedIdentity reservedIdentity = new ReservedIdentity(identityHash, reservedAt);

            Assert.Equal(new string('A', 64), reservedIdentity.IdentityHash);
            Assert.Equal(reservedAt.ToUniversalTime(), reservedIdentity.ReservedAtUtc);
        }

        [Fact]
        public void ReservedIdentity_WithInvalidHash_ThrowsDomainException()
        {
            DomainException exception = Assert.Throws<DomainException>(() => new ReservedIdentity("invalid-hash", DateTimeOffset.UtcNow));

            Assert.Equal("The reserved identity hash must contain exactly 64 hexadecimal characters.", exception.Message);
        }

        [Fact]
        public void LegalDocumentVersion_WithValidValues_CreatesVersion()
        {
            DateTimeOffset publishedAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(2));

            LegalDocumentVersion version = new LegalDocumentVersion(1, LegalDocumentType.TermsOfService, "  1.0  ", publishedAt, true);

            Assert.Equal(1, version.LegalDocumentVersionId);
            Assert.Equal(LegalDocumentType.TermsOfService, version.DocumentType);
            Assert.Equal("1.0", version.VersionNumber);
            Assert.Equal(publishedAt.ToUniversalTime(), version.PublishedAtUtc);
            Assert.True(version.RequiresAcceptance);
        }

        [Fact]
        public void LegalDocumentVersion_WithInvalidType_ThrowsDomainException()
        {
            LegalDocumentType invalidType = (LegalDocumentType)999;

            DomainException exception = Assert.Throws<DomainException>(() => new LegalDocumentVersion(1, invalidType, "1.0", DateTimeOffset.UtcNow, true));

            Assert.Equal("The legal document type is invalid.", exception.Message);
        }

        [Fact]
        public void LegalAcceptance_WithValidValues_CreatesAcceptance()
        {
            Guid userId = Guid.NewGuid();
            DateTimeOffset acceptedAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(2));

            LegalAcceptance acceptance = new LegalAcceptance(userId, 1, acceptedAt);

            Assert.Equal(userId, acceptance.UserId);
            Assert.Equal(1, acceptance.LegalDocumentVersionId);
            Assert.Equal(acceptedAt.ToUniversalTime(), acceptance.AcceptedAtUtc);
        }

        [Fact]
        public void LegalAcceptance_WithEmptyUserId_ThrowsDomainException()
        {
            DomainException exception = Assert.Throws<DomainException>(() => new LegalAcceptance(Guid.Empty, 1, DateTimeOffset.UtcNow));

            Assert.Equal("The user identifier cannot be empty.", exception.Message);
        }
    }
}