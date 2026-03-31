using GRID.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Dashboard
{
    public class IndexModel(ApplicationDbContext db, UserManager<IdentityUser> userManager) : PageModel
    {
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int PendingContactRequests { get; set; }
        public int TotalContactRequests { get; set; }
        public int ActiveInvites { get; set; }
        public int TotalInvites { get; set; }
        public int ActiveServices { get; set; }
        public IList<RecentActivity> RecentAuditLogs { get; set; } = [];
        public IList<DayCount> PageViewChart { get; set; } = [];

        public async Task OnGetAsync()
        {
            TotalUsers = await db.Users.CountAsync();
            var admins = await userManager.GetUsersInRoleAsync("Admin");
            TotalAdmins = admins.Count;

            TotalContactRequests = await db.ContactRequests.CountAsync();
            PendingContactRequests = await db.ContactRequests.CountAsync(r => !r.IsResponded);

            TotalInvites = await db.Invites.CountAsync();
            ActiveInvites = await db.Invites.CountAsync(i => i.IsActive);

            ActiveServices = await db.ServiceLinks.CountAsync(s => s.IsActive);

            var chartStart = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-13), DateTimeKind.Utc);
            var rawCounts = await db.AuditLogs
                .Where(l => l.Action == "PageView" && l.Timestamp >= chartStart)
                .GroupBy(l => new { l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
                .ToListAsync();

            PageViewChart = Enumerable.Range(0, 14)
                .Select(i => chartStart.AddDays(i))
                .Select(d => new DayCount
                {
                    Date = d,
                    Count = rawCounts.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month && r.Day == d.Day)?.Count ?? 0
                })
                .ToList();

            RecentAuditLogs = await db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new RecentActivity
                {
                    ActorEmail = a.ActorEmail,
                    Action = a.Action,
                    EntityId = a.EntityId,
                    Details = a.Details,
                    IpAddress = a.IpAddress,
                    HttpStatus = a.HttpStatus,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();
        }

        public class RecentActivity
        {
            public string? ActorEmail { get; set; }
            public string Action { get; set; } = null!;
            public string? EntityId { get; set; }
            public string? Details { get; set; }
            public string? IpAddress { get; set; }
            public int? HttpStatus { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class DayCount
        {
            public DateTime Date { get; set; }
            public int Count { get; set; }
        }
    }
}
