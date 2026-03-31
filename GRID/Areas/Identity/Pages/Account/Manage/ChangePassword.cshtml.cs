using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager) : PageModel
    {
        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, DataType(DataType.Password), Display(Name = "Current password")]
            public string OldPassword { get; set; } = "";

            [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password), Display(Name = "New password")]
            public string NewPassword { get; set; } = "";

            [DataType(DataType.Password), Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match.")]
            public string ConfirmPassword { get; set; } = "";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var hasPassword = await userManager.HasPasswordAsync(user);
            if (!hasPassword) return RedirectToPage("./SetPassword");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            await signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your password has been changed.";
            return RedirectToPage();
        }
    }
}
