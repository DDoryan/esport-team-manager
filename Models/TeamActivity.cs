using System.ComponentModel.DataAnnotations;

namespace RepriseWeb.Models
{
    public class TeamActivity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le titre est obligatoire.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Le titre doit contenir entre 3 et 100 caractères.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le type est obligatoire.")]
        public string Type { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
    }
}
