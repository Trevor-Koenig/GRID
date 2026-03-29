using System.ComponentModel.DataAnnotations;

namespace GRID.Models
{
    public class ContactRequest
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = null!;

        [Required, MaxLength(200)]
        public string Subject { get; set; } = null!;

        [Required]
        public string Message { get; set; } = null!;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public bool IsResponded { get; set; } = false;

        public DateTime? RespondedAt { get; set; }
    }
}
