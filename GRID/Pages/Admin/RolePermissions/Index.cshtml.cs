using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.RolePermissions
{
    public class IndexModel(
        ApplicationDbContext db,
        RoleManager<IdentityRole> roleManager,
        PermissionService permissionService,
        AuditService auditService) : PageModel
    {
        public List<string> Roles { get; set; } = [];
        public Dictionary<string, HashSet<string>> RolePerms { get; set; } = new();
        public string[] AllPermissions => Permissions.All;
        public Dictionary<string, string> PermissionLabels => Permissions.Labels;

        [TempData] public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            Roles = (await roleManager.Roles.OrderBy(r => r.Name).ToListAsync())
                        .Select(r => r.Name!)
                        .ToList();

            var all = await db.RolePermissions.ToListAsync();
            foreach (var role in Roles)
            {
                RolePerms[role] = all
                    .Where(rp => rp.RoleName.Equals(role, StringComparison.OrdinalIgnoreCase))
                    .Select(rp => rp.Permission)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task<IActionResult> OnPostSaveAsync(string roleName, string[] grantedPermissions)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest();

            var existing = await db.RolePermissions
                .Where(rp => rp.RoleName == roleName)
                .ToListAsync();

            db.RolePermissions.RemoveRange(existing);

            foreach (var perm in grantedPermissions.Distinct())
            {
                if (Permissions.All.Contains(perm))
                    db.RolePermissions.Add(new RolePermission { RoleName = roleName, Permission = perm });
            }

            await db.SaveChangesAsync();
            permissionService.InvalidateCache();

            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var actorEmail = User.Identity?.Name;
            await auditService.LogAsync("UpdateRolePermissions", actorId, actorEmail,
                "Role", roleName, $"Permissions updated: {string.Join(", ", grantedPermissions)}");

            StatusMessage = $"Permissions for '{roleName}' saved.";
            return RedirectToPage();
        }
    }
}
