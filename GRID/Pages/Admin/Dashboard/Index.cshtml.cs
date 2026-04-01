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

        /// <summary>
        /// Minutes behind UTC — matches JavaScript's Date.getTimezoneOffset().
        /// e.g. UTC-8 = 480, UTC+1 = -60.
        /// </summary>
        [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
        public int TzOffset { get; set; } = 0;

        /// <summary>"Today" in the client's local timezone.</summary>
        public DateTime LocalToday { get; set; }

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

            // Derive the client's local "today" from the timezone offset.
            LocalToday = DateTime.UtcNow.AddMinutes(-TzOffset).Date;
            var chartLocalStart = LocalToday.AddDays(-13);

            // Fetch with a 1-day UTC buffer so no edge-of-day views are missed.
            var queryUtcStart = DateTime.SpecifyKind(chartLocalStart.AddDays(-1), DateTimeKind.Utc);
            var rawTimestamps = await db.AuditLogs
                .Where(l => l.Action == "PageView" && l.Timestamp >= queryUtcStart)
                .Select(l => l.Timestamp)
                .ToListAsync();

            PageViewChart = Enumerable.Range(0, 14)
                .Select(i => chartLocalStart.AddDays(i))
                .Select(d => new DayCount
                {
                    Date = d,
                    Count = rawTimestamps.Count(t => t.AddMinutes(-TzOffset).Date == d.Date)
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
