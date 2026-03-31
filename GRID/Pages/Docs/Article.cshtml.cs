using GRID.Data;
using GRID.Models;
using GRID.Services;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Docs
{
    public class ArticleModel(ApplicationDbContext db, IAuthorizationService auth, AuditService audit) : PageModel
    {
        public DocArticle Article { get; set; } = null!;
        public string RenderedContent { get; set; } = "";
        public List<DocArticle> SidebarArticles { get; set; } = [];
        public bool CanViewPrivate { get; set; }
        public bool IsAdmin { get; set; }
        public bool EditMode { get; set; }

        private static readonly MarkdownPipeline Pipeline =
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public async Task<IActionResult> OnGetAsync(string category, string slug, bool edit = false)
        {
            CanViewPrivate = (await auth.AuthorizeAsync(User, null, "CanViewPrivateDocs")).Succeeded;
            IsAdmin = (await auth.AuthorizeAsync(User, null, "ManageDocs")).Succeeded;

            var query = db.DocArticles
                .Where(d => d.Category.ToLower() == category.ToLower() && d.Slug == slug);

            if (!IsAdmin)
                query = query.Where(d => d.IsPublished && (d.IsPublic || CanViewPrivate));

            var article = await query.FirstOrDefaultAsync();
            if (article == null) return NotFound();

            Article = article;
            EditMode = IsAdmin && edit;
            RenderedContent = Markdown.ToHtml(Article.Content, Pipeline);

            var sidebarQuery = db.DocArticles.AsQueryable();
            if (!IsAdmin) sidebarQuery = sidebarQuery.Where(d => d.IsPublished);
            if (!CanViewPrivate) sidebarQuery = sidebarQuery.Where(d => d.IsPublic);

            SidebarArticles = await sidebarQuery
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DisplayOrder)
                .ThenBy(d => d.Title)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync(
            int id, string title, string slug, string category, string content,
            string? serviceToken, bool isPublished, bool isPublic, int displayOrder)
        {
            if (!(await auth.AuthorizeAsync(User, null, "ManageDocs")).Succeeded)
                return Forbid();

            var article = await db.DocArticles.FindAsync(id);
            if (article == null) return NotFound();

            article.Title = title;
            article.Slug = slug.ToLower();
            article.Category = category;
            article.Content = content;
            article.ServiceToken = string.IsNullOrWhiteSpace(serviceToken) ? null : serviceToken;
            article.IsPublished = isPublished;
            article.IsPublic = isPublic;
            article.DisplayOrder = displayOrder;
            article.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Edited doc article", actorId, User.Identity?.Name, "DocArticle", slug, $"Title: {title}");

            return RedirectToPage(new { category = category.ToLower(), slug = slug.ToLower(), edit = true });
        }
    }
}
