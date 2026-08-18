namespace RepriseWeb.Models
{
    public sealed class ErrorViewModel
    {
        public string CorrelationId { get; }

        public bool ShowCorrelationId => !string.IsNullOrWhiteSpace(CorrelationId);

        public ErrorViewModel(string correlationId)
        {
            CorrelationId = correlationId;
        }
    }
}