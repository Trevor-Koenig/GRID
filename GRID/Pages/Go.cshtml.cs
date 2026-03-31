using GRID.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages
{
    public class GoModel(ApplicationDbContext db) : PageModel
    {
        public async Task<IActionResult> OnGetAsync(string token)
        {
            var link = await db.ServiceLinks
                .FirstOrDefaultAsync(s => s.Token == token && s.IsActive);

            if (link == null)
                return NotFound();

            if (link.RequiresAuth && User.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Login",
                    new { area = "Identity", returnUrl = $"/go/{token}" });

            return Redirect(link.Url);
        }
    }
}
