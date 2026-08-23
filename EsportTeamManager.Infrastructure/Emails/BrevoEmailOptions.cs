using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Infrastructure.Emails;

public sealed class BrevoEmailOptions
{
    public const string SectionName = "Brevo";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;

    [Required]
    public string SenderName { get; set; } = string.Empty;
}