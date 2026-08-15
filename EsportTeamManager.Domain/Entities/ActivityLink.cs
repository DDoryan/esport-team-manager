using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities
{
    public sealed class ActivityLink
    {
        public Guid ActivityLinkId { get; private set; }
        public Guid ActivityId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Url { get; private set; } = string.Empty;

        private ActivityLink()
        {
        }

        public ActivityLink(Guid activityId, string name, string url)
        {
            if (activityId == Guid.Empty)
            {
                throw new DomainException("The activity identifier is required.");
            }

            ActivityLinkId = Guid.NewGuid();
            ActivityId = activityId;

            Update(name, url);
        }

        public void Update(string name, string url)
        {
            string normalizedName = name?.Trim() ?? string.Empty;
            string normalizedUrl = url?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new DomainException("The link name is required.");
            }
            if (normalizedName.Length > 100)
            {
                throw new DomainException("The link name cannot exceed 100 characters.");
            }
            if (normalizedUrl.Length > 2048)
            {
                throw new DomainException("The link URL cannot exceed 2048 characters.");
            }
            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new DomainException("The link URL must use HTTP or HTTPS.");
            }

            Name = normalizedName;
            Url = normalizedUrl;
        }
    }
}