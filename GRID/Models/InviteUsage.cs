namespace GRID.Models
{
    public class InviteUsage
    {
        public int Id { get; set; }
        public int InviteId { get; set; }
        public Invite Invite { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    }
}