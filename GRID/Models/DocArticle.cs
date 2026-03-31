using System.ComponentModel.DataAnnotations;

namespace GRID.Models
{
    public class DocArticle
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Slug { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Category { get; set; } = null!;

        public string Content { get; set; } = "";

        [MaxLength(20)]
        public string? ServiceToken { get; set; }

        public bool IsPublished { get; set; } = false;
        public bool IsPublic { get; set; } = true;
        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
