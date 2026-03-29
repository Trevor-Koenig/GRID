using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Invites
{
    public class IndexModel(ApplicationDbContext context, InviteService inviteService, RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager) : PageModel
    {
        public IList<Invite> Invite { get; set; } = [];
        public IList<string> AvailableRoles { get; set; } = [];
        public Dictionary<int, List<string>> RedeemerEmails { get; set; } = [];

        public async Task OnGetAsync()
        {
            Invite = await context.Invites
                .Include(i => i.Usages)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            AvailableRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();

            foreach (var invite in Invite.Where(i => i.Usages.Any()))
            {
                var emails = new List<string>();
                foreach (var usage in invite.Usages)
                {
                    var user = await userManager.FindByIdAsync(usage.UserId);
                    if (user?.Email != null) emails.Add(user.Email);
                }
                RedeemerEmails[invite.Id] = emails;
            }
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
