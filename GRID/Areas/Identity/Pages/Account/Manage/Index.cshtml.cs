using GRID.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel(UserManager<IdentityUser> userManager, ApplicationDbContext db) : PageModel
    {
        [TempData]
        public string? StatusMessage { get; set; }

        public string Email { get; set; } = "";
        public string? Username { get; set; }
        public DateTime? MemberSince { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            Email = user.Email ?? "";
            Username = user.UserName;
            EmailConfirmed = user.EmailConfirmed;
            TwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user);

            var profile = await db.UserProfiles.FindAsync(user.Id);
            MemberSince = profile?.CreatedAt;

            return Page();
        }
    }
}
