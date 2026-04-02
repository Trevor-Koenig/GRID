using GRID.Data;
using GRID.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GRID.Pages.Admin.Users
{
    public class IndexModel(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db,
        IEmailSender emailSender,
        AuditService audit,
        ILogger<IndexModel> logger) : PageModel
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
                var profile = await db.UserProfiles.FindAsync(user.Id);
                var lastLogin = await db.LoginHistories
                    .Where(h => h.UserId == user.Id && h.Succeeded)
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => (DateTime?)h.Timestamp)
                    .FirstOrDefaultAsync();

                result.Add(new UserRoleViewModel
                {
                    Id = user.Id,
                    Email = user.Email!,
                    EmailConfirmed = user.EmailConfirmed,
                    Roles = roles.ToList(),
                    IsDeactivated = profile?.IsDeactivated ?? false,
                    LastLogin = lastLogin,
                    CreatedAt = profile?.CreatedAt
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

            var actor = User.Identity?.Name;
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Changed user role", actorId, actor, "User", userId, $"New role: {selectedRole}");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateAsync(string userId)
        {
            var profile = await db.UserProfiles.FindAsync(userId);
            if (profile == null) return NotFound();

            profile.IsDeactivated = true;
            profile.DeactivatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var actor = User.Identity?.Name;
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Deactivated user", actorId, actor, "User", userId);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReactivateAsync(string userId)
        {
            var profile = await db.UserProfiles.FindAsync(userId);
            if (profile == null) return NotFound();

            profile.IsDeactivated = false;
            profile.DeactivatedAt = null;
            await db.SaveChangesAsync();

            var actor = User.Identity?.Name;
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Reactivated user", actorId, actor, "User", userId);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostPasswordResetAsync(string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
                System.Text.Encoding.UTF8.GetBytes(token));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encoded },
                protocol: Request.Scheme);

            if (string.IsNullOrEmpty(callbackUrl))
            {
                logger.LogError("Failed to generate password reset URL for user {UserId} (scheme: {Scheme}, host: {Host})",
                    userId, Request.Scheme, Request.Host);
                TempData["ErrorMessage"] = "Could not generate a reset link. Check server logs.";
                return RedirectToPage();
            }

            try
            {
                await emailSender.SendEmailAsync(user.Email!, "Reset your GRID password",
                    $"An admin has requested a password reset for your account. <a href='{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(callbackUrl)}'>Click here to reset your password</a>.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                TempData["ErrorMessage"] = $"Failed to send reset email to {user.Email}. Check server logs.";
                return RedirectToPage();
            }

            var actor = User.Identity?.Name;
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await audit.LogAsync("Sent password reset email", actorId, actor, "User", userId, $"To: {user.Email}");

            TempData["SuccessMessage"] = $"Password reset email sent to {user.Email}.";
            return RedirectToPage();
        }
    }

    public class UserRoleViewModel
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool EmailConfirmed { get; set; }
        public List<string> Roles { get; set; } = [];
        public bool IsDeactivated { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
