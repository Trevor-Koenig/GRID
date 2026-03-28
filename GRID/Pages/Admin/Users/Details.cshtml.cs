using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Pages.Admin.Users
{
    public class DetailsModel(UserManager<IdentityUser> userManager) : PageModel
    {
        public string UserId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool EmailConfirmed { get; set; }
        public IList<string> Roles { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            UserId = user.Id;
            Email = user.Email!;
            EmailConfirmed = user.EmailConfirmed;
            Roles = await userManager.GetRolesAsync(user);

            return Page();
        }
    }
}
