using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class MatchDetail
{
    public Guid ActivityId { get; private set; }

    public string? OpponentName { get; private set; }

    public int? TeamScore { get; private set; }

    public int? OpponentScore { get; private set; }

    public bool HasCompleteScore => TeamScore.HasValue && OpponentScore.HasValue;

    private MatchDetail()
    {
    }

    public MatchDetail(Guid activityId, string? opponentName = null)
    {
        if (activityId == Guid.Empty)
        {
            throw new DomainException("The activity identifier cannot be empty.");
        }

        ActivityId = activityId;
        UpdateOpponent(opponentName);
    }

    public void UpdateOpponent(string? opponentName)
    {
        string? normalizedOpponentName = string.IsNullOrWhiteSpace(opponentName) ? null : opponentName.Trim();

        if (normalizedOpponentName is not null && normalizedOpponentName.Length > 100)
        {
            throw new DomainException("The opponent name cannot exceed 100 characters.");
        }

        OpponentName = normalizedOpponentName;
    }

    public void SetScores(int teamScore, int opponentScore)
    {
        if (teamScore < 0 || opponentScore < 0)
        {
            throw new DomainException("Match scores cannot be negative.");
        }

        TeamScore = teamScore;
        OpponentScore = opponentScore;
    }

    public void ClearScores()
    {
        TeamScore = null;
        OpponentScore = null;
    }

    public void EnsureReadyForCompletion()
    {
        if (!HasCompleteScore)
        {
            throw new DomainException("Both match scores are required to complete the activity.");
        }
    }
}