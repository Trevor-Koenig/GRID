using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.AuditLog
{
    public class IndexModel(ApplicationDbContext db) : PageModel
    {
        public IList<Models.AuditLog> Logs { get; set; } = [];

        public async Task OnGetAsync()
        {
            Logs = await db.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(500)
                .ToListAsync();
        }
    }
}
