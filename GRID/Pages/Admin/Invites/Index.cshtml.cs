using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Invites
{
    public class IndexModel(ApplicationDbContext context, InviteService inviteService, RoleManager<IdentityRole> roleManager) : PageModel
    {
        public IList<Invite> Invite { get; set; } = [];
        public IList<string> AvailableRoles { get; set; } = [];

        public async Task OnGetAsync()
        {
            Invite = await context.Invites.OrderByDescending(i => i.CreatedAt).ToListAsync();
            AvailableRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
        }

        public async Task<IActionResult> OnPostCreateAsync(string? role, bool isSingleUse, int? maxUses, string? email, DateTime? expiresAt)
        {
            await inviteService.CreateInviteAsync(role, isSingleUse, maxUses, email, expiresAt);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var invite = await context.Invites.FindAsync(id);
            if (invite != null)
            {
                context.Invites.Remove(invite);
                await context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
