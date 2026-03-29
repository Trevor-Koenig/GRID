namespace GRID.Models
{
    public class LoginHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public string UserEmail { get; set; } = null!;
        public bool Succeeded { get; set; }
        public string? IpAddress { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
