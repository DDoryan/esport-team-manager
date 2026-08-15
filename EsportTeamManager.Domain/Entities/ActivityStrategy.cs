using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities
{
    public sealed class ActivityStrategy
    {
        public Guid ActivityId { get; private set; }
        public Guid StrategyId { get; private set; }

        private ActivityStrategy()
        {
        }

        public ActivityStrategy(Guid activityId, Guid strategyId)
        {
            if (activityId == Guid.Empty)
            {
                throw new DomainException("The activity identifier is required.");
            }
            if (strategyId == Guid.Empty)
            {
                throw new DomainException("The strategy identifier is required.");
            }

            ActivityId = activityId;
            StrategyId = strategyId;
        }
    }
}