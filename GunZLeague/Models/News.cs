namespace GunZLeague.Models
{
    /// <summary>
    /// Representa uma notícia do site
    /// </summary>
    public class News
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsPublished { get; set; } = true;
    }
}