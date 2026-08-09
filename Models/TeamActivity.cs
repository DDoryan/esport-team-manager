namespace RepriseWeb.Models
{
    public class TeamActivity
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
    }
}
