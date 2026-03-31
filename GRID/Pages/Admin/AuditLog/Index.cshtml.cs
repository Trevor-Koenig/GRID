using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.AuditLog
{
    public class IndexModel(ApplicationDbContext db) : PageModel
    {
        public IList<Models.AuditLog> Logs { get; set; } = [];

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "all";

        public async Task OnGetAsync()
        {
            var query = db.AuditLogs.AsQueryable();

            query = Filter switch
            {
                "pageviews" => query.Where(l => l.Action == "PageView"),
                "admin"     => query.Where(l => l.Action != "PageView"),
                _           => query
            };

            Logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(500)
                .ToListAsync();
        }
    }
}
