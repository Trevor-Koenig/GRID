using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages
{
    public class IndexModel(ApplicationDbContext db) : PageModel
    {
        [BindProperty]
        public ContactRequest ContactInput { get; set; } = new();

        public bool ContactSent { get; set; }
        public IList<ServiceLink> HeroServices { get; set; } = [];
        public IList<ServiceLink> ServicesSection { get; set; } = [];

        public async Task OnGetAsync()
        {
            ContactSent = TempData["ContactSent"] as bool? ?? false;
            var active = await db.ServiceLinks
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();
            HeroServices = active.Where(s => s.ShowInHero).ToList();
            ServicesSection = active.Where(s => s.ShowInServices).ToList();
        }

        [EnableRateLimiting("ContactLimiter")]
        public async Task<IActionResult> OnPostContactAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            ContactInput.SubmittedAt = DateTime.UtcNow;
            db.ContactRequests.Add(ContactInput);
            await db.SaveChangesAsync();

            TempData["ContactSent"] = true;
            return RedirectToPage(null, null, null, "contact");
        }
    }
}
