using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Areas.Identity.Pages.Account.Manage
{
    public class TwoFactorAuthenticationModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager) : PageModel
    {
        [TempData]
        public string? StatusMessage { get; set; }

        public bool HasAuthenticator { get; set; }
        public bool Is2faEnabled { get; set; }
        public bool IsMachineRemembered { get; set; }
        public int RecoveryCodesLeft { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            HasAuthenticator = await userManager.GetAuthenticatorKeyAsync(user) != null;
            Is2faEnabled = await userManager.GetTwoFactorEnabledAsync(user);
            IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user);
            RecoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            await signInManager.ForgetTwoFactorClientAsync();
            StatusMessage = "The current browser has been forgotten. You will be prompted for your 2FA code next time you log in from this browser.";
            return RedirectToPage();
        }
    }
}
