using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Pages.Admin.Users
{
    public class IndexModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) : PageModel
    {
        public IList<UserRoleViewModel> Users { get; set; } = [];
        public IList<string> AvailableRoles { get; set; } = [];

        public async Task OnGetAsync()
        {
            var users = userManager.Users.OrderBy(u => u.Email).ToList();
            var result = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                result.Add(new UserRoleViewModel
                {
                    Id = user.Id,
                    Email = user.Email!,
                    EmailConfirmed = user.EmailConfirmed,
                    Roles = roles.ToList()
                });
            }

            Users = result;
            AvailableRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
        }

        public async Task<IActionResult> OnPostEditRoleAsync(string userId, string selectedRole)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(selectedRole) && await roleManager.RoleExistsAsync(selectedRole))
                await userManager.AddToRoleAsync(user, selectedRole);

            return RedirectToPage();
        }
    }

    public class UserRoleViewModel
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool EmailConfirmed { get; set; }
        public List<string> Roles { get; set; } = [];
    }
}
