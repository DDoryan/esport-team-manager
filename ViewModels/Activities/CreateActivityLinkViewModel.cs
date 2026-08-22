using System.ComponentModel.DataAnnotations;

namespace RepriseWeb.ViewModels.Activities;

public sealed class CreateActivityLinkViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Le nom du lien est obligatoire.")]
    [StringLength(100, ErrorMessage = "Le nom du lien ne peut pas dépasser 100 caractères.")]
    [Display(Name = "Nom du lien")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "L’URL du lien est obligatoire.")]
    [StringLength(2048, ErrorMessage = "L’URL du lien ne peut pas dépasser 2 048 caractères.")]
    [Display(Name = "URL")]
    public string? Url { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            yield break;
        }

        if (!Uri.TryCreate(Url.Trim(), UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult("L’URL doit utiliser le protocole HTTP ou HTTPS.", [nameof(Url)]);
        }
    }
}