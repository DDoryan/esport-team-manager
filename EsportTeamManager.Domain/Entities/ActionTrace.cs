using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities
{
    public sealed class ActionTrace
    {
        public Guid ActionTraceId { get; private set; }
        public Guid? ActorUserId { get; private set; }
        public Guid? TeamId { get; private set; }
        public string ActionCode { get; private set; } = string.Empty;
        public string ObjectType { get; private set; } = string.Empty;
        public string? ObjectIdentifier { get; private set; }
        public TraceOutcome Outcome { get; private set; }
        public DateTimeOffset OccurredAtUtc { get; private set; }
        public DateTimeOffset ExpiresAtUtc { get; private set; }

        private ActionTrace()
        {
        }

        public ActionTrace(Guid? actorUserId, Guid? teamId, string actionCode, string objectType, string? objectIdentifier, TraceOutcome outcome, DateTimeOffset occurredAtUtc)
        {
            if (actorUserId.HasValue && actorUserId.Value == Guid.Empty)
            {
                throw new DomainException("The actor identifier is invalid.");
            }

            if (teamId.HasValue && teamId.Value == Guid.Empty)
            {
                throw new DomainException("The team identifier is invalid.");
            }

            string normalizedActionCode = actionCode?.Trim() ?? string.Empty;
            string normalizedObjectType = objectType?.Trim() ?? string.Empty;
            string? normalizedObjectIdentifier = NormalizeOptionalText(objectIdentifier);

            if (string.IsNullOrWhiteSpace(normalizedActionCode) || normalizedActionCode.Length > 60)
            {
                throw new DomainException("The action code is required and cannot exceed 60 characters.");
            }

            if (string.IsNullOrWhiteSpace(normalizedObjectType) || normalizedObjectType.Length > 60)
            {
                throw new DomainException("The object type is required and cannot exceed 60 characters.");
            }

            if (normalizedObjectIdentifier?.Length > 64)
            {
                throw new DomainException("The object identifier cannot exceed 64 characters.");
            }

            if (!Enum.IsDefined(typeof(TraceOutcome), outcome))
            {
                throw new DomainException("The trace outcome is invalid.");
            }

            if (occurredAtUtc == default)
            {
                throw new DomainException("The occurrence date is required.");
            }

            ActionTraceId = Guid.NewGuid();
            ActorUserId = actorUserId;
            TeamId = teamId;
            ActionCode = normalizedActionCode;
            ObjectType = normalizedObjectType;
            ObjectIdentifier = normalizedObjectIdentifier;
            Outcome = outcome;
            OccurredAtUtc = occurredAtUtc.ToUniversalTime();
            ExpiresAtUtc = OccurredAtUtc.AddMonths(6);
        }

        public void RemoveActor()
        {
            ActorUserId = null;
        }

        public void RemoveTeam()
        {
            TeamId = null;
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