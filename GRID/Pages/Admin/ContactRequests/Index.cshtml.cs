using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.ContactRequests
{
    public class IndexModel(ApplicationDbContext db, IEmailSender emailSender) : PageModel
    {
        public IList<ContactRequest> Requests { get; set; } = [];

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        public async Task OnGetAsync()
        {
            var query = db.ContactRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(Search))
                query = query.Where(r =>
                    r.Name.Contains(Search) ||
                    r.Email.Contains(Search) ||
                    r.Subject.Contains(Search));

            if (Status == "pending")
                query = query.Where(r => !r.IsResponded);
            else if (Status == "responded")
                query = query.Where(r => r.IsResponded);

            Requests = await query.OrderByDescending(r => r.SubmittedAt).ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var request = await db.ContactRequests.FindAsync(id);
            if (request != null)
            {
                db.ContactRequests.Remove(request);
                await db.SaveChangesAsync();
            }
            return RedirectToPage(new { Search, Status });
        }

        public async Task<IActionResult> OnPostRespondAsync(int id)
        {
            var request = await db.ContactRequests.FindAsync(id);
            if (request != null)
            {
                request.IsResponded = true;
                request.RespondedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return RedirectToPage(new { Search, Status });
        }

        public async Task<IActionResult> OnPostUnrespondAsync(int id)
        {
            var request = await db.ContactRequests.FindAsync(id);
            if (request != null)
            {
                request.IsResponded = false;
                request.RespondedAt = null;
                await db.SaveChangesAsync();
            }
            return RedirectToPage(new { Search, Status });
        }

        public async Task<IActionResult> OnPostReplyAsync(int id, string replyMessage)
        {
            var request = await db.ContactRequests.FindAsync(id);
            if (request == null) return NotFound();

            var html = $"""
                <p>Hi {System.Net.WebUtility.HtmlEncode(request.Name)},</p>
                <p>{System.Net.WebUtility.HtmlEncode(replyMessage).Replace("\n", "<br>")}</p>
                <hr>
                <p style="color:#888;font-size:12px;">
                    This is a reply to your message: <em>{System.Net.WebUtility.HtmlEncode(request.Subject)}</em>
                </p>
                """;

            await emailSender.SendEmailAsync(request.Email, $"Re: {request.Subject}", html);

            request.IsResponded = true;
            request.RespondedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return RedirectToPage(new { Search, Status });
        }
    }
}
