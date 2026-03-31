using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Roles
{
    public class IndexModel(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager) : PageModel
    {
        public IList<RoleViewModel> Roles { get; set; } = [];

        public async Task OnGetAsync()
        {
            var roles = await roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            var result = new List<RoleViewModel>();

            foreach (var role in roles)
            {
                var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
                result.Add(new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name!,
                    UserCount = usersInRole.Count
                });
            }

            Roles = result;
        }

        public async Task<IActionResult> OnPostCreateAsync(string roleName)
        {
            if (!string.IsNullOrWhiteSpace(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName.Trim()));

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string roleId)
        {
            var role = await roleManager.FindByIdAsync(roleId);
            if (role != null)
                await roleManager.DeleteAsync(role);

            return RedirectToPage();
        }
    }

    public class RoleViewModel
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int UserCount { get; set; }
    }
}
