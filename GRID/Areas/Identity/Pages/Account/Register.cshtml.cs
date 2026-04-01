// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace GRID.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly InviteService _inviteService;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            InviteService inviteService,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            IWebHostEnvironment env,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _inviteService = inviteService;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _env = env;
            _db = db;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; } = new();

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        // Records whether or not the link had an invite code when it started so that we do not make input read-only on incorrect code submit
        [BindProperty]
        public bool InviteFromLink { get; set; }

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
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }


            [Required(ErrorMessage = "You must have an invite code to Register")]
            [Display(Name = "Invite code")]
            public string InviteCode { get; set; }
        }


        public async Task OnGetAsync(string returnUrl = null, string? inviteCode = null)
        {
            ReturnUrl = returnUrl;
            if (!string.IsNullOrWhiteSpace(inviteCode))
            {
                InviteFromLink = true;
                Input.InviteCode = inviteCode;
            }
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {

                /******************
                 *
                 * Custom Invite code validation logic
                 *
                 ******************/

                // In development, auto-create the invite code if it doesn't already exist
                if (_env.IsDevelopment())
                    await _inviteService.EnsureDevInviteAsync(Input.InviteCode);

                var (isValid, invite) = await _inviteService.ValidateInviteAsync(Input.InviteCode);
                if (!string.IsNullOrEmpty(Input.InviteCode))
                {
                    if (isValid != 0 || invite == null)
                    {
                        switch (isValid)
                        {
                            case 1:
                                ModelState.AddModelError("Input.InviteCode", "Invite code does not exist.");
                                break;
                            case 2:
                                ModelState.AddModelError("Input.InviteCode", "Invite code expired.");
                                break;
                            case 3:
                                ModelState.AddModelError("Input.InviteCode", "Invite code has already been used.");
                                break;
                            case 4:
                                ModelState.AddModelError("Input.InviteCode", "Invite code has reached max uses.");
                                break;
                            default:
                                if (invite == null)
                                {
                                    ModelState.AddModelError("Input.InviteCode", "Invite does not exist.");
                                }
                                else
                                {
                                    ModelState.AddModelError("Input.InviteCode", "Invalid or expired invite code.");
                                }
                                break;
                        }
                        return Page();
                    }
                }

                // end invite code validation logic

                var user = CreateUser();
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    // mark invite code as used by this user (only after user is saved to DB)
                    var consumeResult = await _inviteService.ConsumeInviteAsync(Input.InviteCode, user.Id);
                    if (!consumeResult.Success)
                    {
                        await _userManager.DeleteAsync(user);

                        ModelState.AddModelError(
                            string.Empty,
                            "Invite code could not be consumed. Please try again.");

                        return Page();
                    }

                    _logger.LogInformation("User created a new account with password.");

                    // In development: first account created becomes Admin, all others become User
                    // In production: role comes from invite code, defaulting to User
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

                    // Create user profile
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
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
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
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
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
