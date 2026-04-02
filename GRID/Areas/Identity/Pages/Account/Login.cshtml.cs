// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using GRID.Data;
using GRID.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GRID.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("LoginLimiter")]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, ApplicationDbContext db, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Check deactivation before attempting sign-in
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user != null)
                {
                    var profile = await _db.UserProfiles.FindAsync(user.Id);
                    if (profile?.IsDeactivated == true)
                    {
                        ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact an administrator.");
                        return Page();
                    }
                }

                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                var rawIp = HttpContext.Connection.RemoteIpAddress;
                var ip = rawIp?.IsIPv4MappedToIPv6 == true ? rawIp.MapToIPv4().ToString() : rawIp?.ToString();

                // Record login history
                if (user != null)
                {
                    _db.LoginHistories.Add(new LoginHistory
                    {
                        UserId = user.Id,
                        UserEmail = Input.Email,
                        Succeeded = result.Succeeded,
                        IpAddress = ip,
                        Timestamp = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }

                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    _db.AuditLogs.Add(new AuditLog
                    {
                        Action = "FailedLogin",
                        ActorEmail = Input.Email,
                        IpAddress = ip,
                        Details = "Account locked out"
                    });
                    await _db.SaveChangesAsync();
                    return RedirectToPage("./Lockout");
                }
                else if (result.IsNotAllowed && user != null && !await _userManager.IsEmailConfirmedAsync(user))
                {
                    _db.AuditLogs.Add(new AuditLog
                    {
                        Action = "FailedLogin",
                        ActorEmail = Input.Email,
                        IpAddress = ip,
                        Details = "Email not confirmed"
                    });
                    await _db.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "You must confirm your email before logging in. Check your inbox, or use the \"Resend email confirmation\" link below.");
                    return Page();
                }
                else
                {
                    _db.AuditLogs.Add(new AuditLog
                    {
                        Action = "FailedLogin",
                        ActorEmail = Input.Email,
                        IpAddress = ip,
                        Details = user == null ? "Email not found" : "Wrong password"
                    });
                    await _db.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
