using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Pages.Admin.Users
{
    public class EditModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) : PageModel
    {
        [BindProperty]
        public string UserId { get; set; } = null!;

        [BindProperty]
        public string Email { get; set; } = null!;

        [BindProperty]
        public string SelectedRole { get; set; } = null!;

        public List<string> AvailableRoles { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            UserId = user.Id;
            Email = user.Email!;
            AvailableRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();

            var currentRoles = await userManager.GetRolesAsync(user);
            SelectedRole = currentRoles.FirstOrDefault() ?? string.Empty;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await userManager.FindByIdAsync(UserId);
            if (user == null) return NotFound();

            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(SelectedRole) && await roleManager.RoleExistsAsync(SelectedRole))
                await userManager.AddToRoleAsync(user, SelectedRole);

            return RedirectToPage("./Index");
        }
    }
}
