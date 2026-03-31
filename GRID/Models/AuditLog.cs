namespace GRID.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string? ActorId { get; set; }
        public string? ActorEmail { get; set; }
        public string Action { get; set; } = null!;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int? DurationSeconds { get; set; }
        public string? IpAddress { get; set; }
        public int? HttpStatus { get; set; }
    }
}
