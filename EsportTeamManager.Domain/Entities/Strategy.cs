using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities
{
    public sealed class Strategy
    {
        public Guid StrategyId { get; private set; }
        public Guid TeamId { get; private set; }
        public Guid CreatedByMembershipId { get; private set; }
        public int MapId { get; private set; }
        public Map Map { get; private set; } = null!;
        public string Name { get; private set; } = string.Empty;
        public StrategySide Side { get; private set; }
        public string? Description { get; private set; }
        public string? ExternalUrl { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAtUtc { get; private set; }
        public DateTimeOffset UpdatedAtUtc { get; private set; }

        private Strategy()
        {
        }

        public Strategy(Guid teamId, Guid createdByMembershipId, int mapId, string name, StrategySide side, string? description, string? externalUrl, DateTimeOffset nowUtc)
        {
            if (teamId == Guid.Empty)
            {
                throw new DomainException("The team identifier is required.");
            }

            if (createdByMembershipId == Guid.Empty)
            {
                throw new DomainException("The creator membership identifier is required.");
            }

            StrategyId = Guid.NewGuid();
            TeamId = teamId;
            CreatedByMembershipId = createdByMembershipId;
            IsActive = true;
            CreatedAtUtc = nowUtc;
            UpdatedAtUtc = nowUtc;

            ChangeMap(mapId, nowUtc);
            Rename(name, nowUtc);
            ChangeSide(side, nowUtc);
            UpdateContent(description, externalUrl, nowUtc);
        }

        public void Rename(string name, DateTimeOffset nowUtc)
        {
            string normalizedName = name?.Trim() ?? string.Empty;

            if (normalizedName.Length < 3 || normalizedName.Length > 100)
            {
                throw new DomainException("The strategy name must contain between 3 and 100 characters.");
            }

            Name = normalizedName;
            UpdatedAtUtc = nowUtc;
        }

        public void ChangeMap(int mapId, DateTimeOffset nowUtc)
        {
            if (mapId <= 0)
            {
                throw new DomainException("The map identifier is required.");
            }

            MapId = mapId;
            UpdatedAtUtc = nowUtc;
        }

        public void ChangeSide(StrategySide side, DateTimeOffset nowUtc)
        {
            if (!Enum.IsDefined(typeof(StrategySide), side))
            {
                throw new DomainException("The strategy side is invalid.");
            }

            Side = side;
            UpdatedAtUtc = nowUtc;
        }

        public void UpdateContent(string? description, string? externalUrl, DateTimeOffset nowUtc)
        {
            string? normalizedDescription = NormalizeOptionalText(description);
            string? normalizedExternalUrl = NormalizeOptionalText(externalUrl);

            if (normalizedDescription?.Length > 5000)
            {
                throw new DomainException("The strategy description cannot exceed 5000 characters.");
            }

            if (normalizedExternalUrl?.Length > 2048)
            {
                throw new DomainException("The strategy URL cannot exceed 2048 characters.");
            }

            if (normalizedExternalUrl is not null && (!Uri.TryCreate(normalizedExternalUrl, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                throw new DomainException("The strategy URL must use HTTP or HTTPS.");
            }

            Description = normalizedDescription;
            ExternalUrl = normalizedExternalUrl;
            UpdatedAtUtc = nowUtc;
        }

        public void EnsureHasContent(bool hasImage)
        {
            if (Description is null && ExternalUrl is null && !hasImage)
            {
                throw new DomainException("A strategy must contain a description, an external URL or an image.");
            }
        }

        public void SetActive(bool isActive, DateTimeOffset nowUtc)
        {
            IsActive = isActive;
            UpdatedAtUtc = nowUtc;
        }

        private static string? NormalizeOptionalText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}