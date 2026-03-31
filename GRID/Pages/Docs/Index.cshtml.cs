using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Docs
{
    public class IndexModel(ApplicationDbContext db, IAuthorizationService auth, AuditService audit) : PageModel
    {
        public List<IGrouping<string, DocArticle>> GroupedArticles { get; set; } = [];
        public bool CanViewPrivate { get; set; }
        public bool IsAdmin { get; set; }

        public async Task OnGetAsync()
        {
            CanViewPrivate = (await auth.AuthorizeAsync(User, null, "CanViewPrivateDocs")).Succeeded;
            IsAdmin = (await auth.AuthorizeAsync(User, null, "ManageDocs")).Succeeded;

            var query = db.DocArticles.AsQueryable();
            if (!IsAdmin) query = query.Where(d => d.IsPublished);
            if (!CanViewPrivate) query = query.Where(d => d.IsPublic);

            GroupedArticles = await query
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DisplayOrder)
                .ThenBy(d => d.Title)
                .GroupBy(d => d.Category)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string title, string slug, string category, string content,
            string? serviceToken, bool isPublished, bool isPublic, int displayOrder)
        {
            if (!(await auth.AuthorizeAsync(User, null, "ManageDocs")).Succeeded)
                return Forbid();

            var now = DateTime.UtcNow;
            db.DocArticles.Add(new DocArticle
            {
                Title = title,
                Slug = slug.ToLower(),
                Category = category,
                Content = content,
                ServiceToken = string.IsNullOrWhiteSpace(serviceToken) ? null : serviceToken,
                IsPublished = isPublished,
                IsPublic = isPublic,
                DisplayOrder = displayOrder,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();

            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Created doc article", actorId, User.Identity?.Name, "DocArticle", slug, $"Title: {title}, Category: {category}");

            return RedirectToPage("/Docs/Article", new { category = category.ToLower(), slug = slug.ToLower(), edit = true });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (!(await auth.AuthorizeAsync(User, null, "ManageDocs")).Succeeded)
                return Forbid();

            var article = await db.DocArticles.FindAsync(id);
            if (article != null)
            {
                var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                await audit.LogAsync("Deleted doc article", actorId, User.Identity?.Name, "DocArticle", article.Slug, $"Title: {article.Title}");
                db.DocArticles.Remove(article);
                await db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
