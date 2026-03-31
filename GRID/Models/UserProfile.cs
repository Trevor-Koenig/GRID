using System.ComponentModel.DataAnnotations;

namespace GRID.Models
{
    public class UserProfile
    {
        [Key]
        public string UserId { get; set; } = null!;
        public bool IsDeactivated { get; set; }
        public DateTime? DeactivatedAt { get; set; }

        [MaxLength(10)]
        public string? Theme { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
