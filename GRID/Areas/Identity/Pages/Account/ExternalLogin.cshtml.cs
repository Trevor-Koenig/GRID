// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GRID.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("LoginLimiter")]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly InviteService _inviteService;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public ExternalLoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender,
            InviteService inviteService,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env,
            ApplicationDbContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
            _inviteService = inviteService;
            _roleManager = roleManager;
            _env = env;
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        // Set when the provider's email already belongs to a GRID account, so the user
        // is told to sign in and link the provider instead of registering a duplicate.
        public bool EmailAlreadyRegistered { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "You must have an invite code to Register")]
            [Display(Name = "Invite code")]
            public string InviteCode { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var ip = GetClientIp();
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                var profile = await _db.UserProfiles.FindAsync(user.Id);
                if (profile?.IsDeactivated == true)
                {
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                    ErrorMessage = "This account has been deactivated. Please contact an administrator.";
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }
            }

            // Sign in the user with this external login provider if the user already has a login.
            // Two-factor is not bypassed so accounts with 2FA (required for admins) still get challenged.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);

            if (user != null)
            {
                _db.LoginHistories.Add(new LoginHistory
                {
                    UserId = user.Id,
                    UserEmail = user.Email,
                    Succeeded = result.Succeeded,
                    IpAddress = ip,
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = false });
            }
            if (result.IsLockedOut)
            {
                await LogFailedLoginAsync(user?.Email, ip, $"Account locked out ({info.ProviderDisplayName})");
                return RedirectToPage("./Lockout");
            }
            if (user != null)
            {
                // Login is linked but sign-in was not allowed — in practice an unconfirmed email.
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                await LogFailedLoginAsync(user.Email, ip, $"Email not confirmed ({info.ProviderDisplayName})");
                ErrorMessage = "You must confirm your email before logging in. Check your inbox, or use the \"Resend email confirmation\" link below.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // No account is linked to this provider login, so ask the user to register with an invite code.
            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName;
            var providerEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrEmpty(providerEmail))
            {
                Input.Email = providerEmail;
                EmailAlreadyRegistered = await _userManager.FindByEmailAsync(providerEmail) != null;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return Page();

            // Never auto-link to an existing account: the user must prove ownership by
            // signing in first, then link the provider from Manage > External Logins.
            if (await _userManager.FindByEmailAsync(Input.Email) != null)
            {
                EmailAlreadyRegistered = true;
                return Page();
            }

            // In development, auto-create the invite code if it doesn't already exist
            if (_env.IsDevelopment())
                await _inviteService.EnsureDevInviteAsync(Input.InviteCode);

            var (isValid, invite) = await _inviteService.ValidateInviteAsync(Input.InviteCode);
            if (isValid != 0 || invite == null)
            {
                ModelState.AddModelError("Input.InviteCode", InviteService.DescribeValidationError(isValid));
                return Page();
            }

            var user = CreateUser();
            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                result = await _userManager.AddLoginAsync(user, info);
                if (!result.Succeeded)
                    await _userManager.DeleteAsync(user);
            }
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            // mark invite code as used by this user (only after user is saved to DB)
            var consumeResult = await _inviteService.ConsumeInviteAsync(Input.InviteCode, user.Id);
            if (!consumeResult.Success)
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Invite code could not be consumed. Please try again.");
                return Page();
            }

            _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

            // Same role rules as Register: first dev account is Admin, otherwise the invite's role
            string roleToAssign;
            if (_env.IsDevelopment())
            {
                var isFirstUser = await _userManager.Users.CountAsync() == 1;
                roleToAssign = isFirstUser ? "Admin" : "User";
            }
            else
            {
                roleToAssign = !string.IsNullOrEmpty(invite.Role) && await _roleManager.RoleExistsAsync(invite.Role)
                    ? invite.Role
                    : "User";
            }
            await _userManager.AddToRoleAsync(user, roleToAssign);

            _db.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            if (_userManager.Options.SignIn.RequireConfirmedAccount)
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return RedirectToPage("./RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
            return LocalRedirect(returnUrl);
        }

        private async Task LogFailedLoginAsync(string email, string ip, string details)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "FailedLogin",
                ActorEmail = email,
                IpAddress = ip,
                Details = details
            });
            await _db.SaveChangesAsync();
        }

        private string GetClientIp()
        {
            var rawIp = HttpContext.Connection.RemoteIpAddress;
            return rawIp?.IsIPv4MappedToIPv6 == true ? rawIp.MapToIPv4().ToString() : rawIp?.ToString();
        }

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
