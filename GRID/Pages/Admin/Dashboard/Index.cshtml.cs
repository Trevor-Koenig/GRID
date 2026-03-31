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

            RecentAuditLogs = await db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new RecentActivity
                {
                    ActorEmail = a.ActorEmail ?? "System",
                    Action = a.Action,
                    Details = a.Details,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();
        }

        public class RecentActivity
        {
            public string ActorEmail { get; set; } = null!;
            public string Action { get; set; } = null!;
            public string? Details { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
