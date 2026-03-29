using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Pages
{
    public class IndexModel(ApplicationDbContext db) : PageModel
    {
        [BindProperty]
        public ContactRequest ContactInput { get; set; } = new();

        public bool ContactSent { get; set; }

        public void OnGet()
        {
            ContactSent = TempData["ContactSent"] as bool? ?? false;
        }

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
