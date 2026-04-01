using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.AuditLog
{
    public class IndexModel(ApplicationDbContext db) : PageModel
    {
        public const int PageSize = 50;

        public IList<Models.AuditLog> Logs { get; set; } = [];
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public async Task OnGetAsync()
        {
            if (CurrentPage < 1) CurrentPage = 1;

            var query = db.AuditLogs.AsQueryable();

            query = Filter switch
            {
                "pageviews" => query.Where(l => l.Action == "PageView"),
                "admin"     => query.Where(l => l.Action != "PageView"),
                _           => query
            };

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var pattern = $"%{Search}%";
                query = query.Where(l =>
                    EF.Functions.ILike(l.ActorEmail ?? "", pattern) ||
                    EF.Functions.ILike(l.IpAddress ?? "", pattern));
            }

            TotalCount = await query.CountAsync();

            Logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
