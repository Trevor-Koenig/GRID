using System.ComponentModel.DataAnnotations;

namespace GRID.Models
{
    public class ServiceLink
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(20)]
        public string Token { get; set; } = null!;

        [Required]
        public string Url { get; set; } = null!;

        [MaxLength(100)]
        public string? IconClass { get; set; }

        public string? Description { get; set; }

        public bool RequiresAuth { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool ShowInNav { get; set; } = true;
        public bool ShowInHero { get; set; } = true;
        public bool ShowInServices { get; set; } = true;
        public int DisplayOrder { get; set; }
    }
}
