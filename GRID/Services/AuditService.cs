using GRID.Data;
using GRID.Models;

namespace GRID.Services
{
    public class AuditService(ApplicationDbContext db)
    {
        public async Task LogAsync(string action, string? actorId = null, string? actorEmail = null,
            string? entityType = null, string? entityId = null, string? details = null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = action,
                ActorId = actorId,
                ActorEmail = actorEmail,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
