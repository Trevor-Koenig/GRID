using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Services
{
    public class IndexModel(ApplicationDbContext db, IServiceStatusService statusService, AuditService audit) : PageModel
    {
        public IList<ServiceLink> Links { get; set; } = [];
        public IServiceStatusService StatusService { get; } = statusService;

        public async Task OnGetAsync()
        {
            Links = await db.ServiceLinks.OrderBy(s => s.DisplayOrder).ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name, string token, string url,
            string? iconClass, string? description, bool requiresAuth, bool showInNav, bool showOnHomePage)
        {
            var maxOrder = await db.ServiceLinks.MaxAsync(s => (int?)s.DisplayOrder) ?? 0;
            db.ServiceLinks.Add(new ServiceLink
            {
                Name = name,
                Token = token,
                Url = url,
                IconClass = iconClass,
                Description = description,
                RequiresAuth = requiresAuth,
                ShowInNav = showInNav,
                ShowOnHomePage = showOnHomePage,
                IsActive = true,
                DisplayOrder = maxOrder + 1
            });
            await db.SaveChangesAsync();

            var actor = User.Identity?.Name;
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Created service link", actorId, actor, "ServiceLink", token, $"Name: {name}, URL: {url}");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync(int id, string name, string token, string url,
            string? iconClass, string? description, bool requiresAuth, bool isActive, bool showInNav, bool showOnHomePage)
        {
            var link = await db.ServiceLinks.FindAsync(id);
            if (link == null) return NotFound();

            link.Name = name;
            link.Token = token;
            link.Url = url;
            link.IconClass = iconClass;
            link.Description = description;
            link.RequiresAuth = requiresAuth;
            link.IsActive = isActive;
            link.ShowInNav = showInNav;
            link.ShowOnHomePage = showOnHomePage;
            await db.SaveChangesAsync();

            var actor = User.Identity?.Name;
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Edited service link", actorId, actor, "ServiceLink", token, $"Name: {name}");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var link = await db.ServiceLinks.FindAsync(id);
            if (link != null)
            {
                var actor = User.Identity?.Name;
                var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                await audit.LogAsync("Deleted service link", actorId, actor, "ServiceLink", link.Token, $"Name: {link.Name}");
                db.ServiceLinks.Remove(link);
                await db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
