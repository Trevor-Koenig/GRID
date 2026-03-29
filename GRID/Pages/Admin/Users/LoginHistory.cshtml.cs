using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Users
{
    public class LoginHistoryModel(ApplicationDbContext db, UserManager<IdentityUser> userManager) : PageModel
    {
        public string UserEmail { get; set; } = null!;
        public IList<LoginHistory> History { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            UserEmail = user.Email!;
            History = await db.LoginHistories
                .Where(h => h.UserId == id)
                .OrderByDescending(h => h.Timestamp)
                .Take(100)
                .ToListAsync();

            return Page();
        }
    }
}
