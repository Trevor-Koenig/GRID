using Microsoft.AspNetCore.Identity;

namespace GRID.Middleware
{
    public class AdminTwoFactorMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, UserManager<IdentityUser> userManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var user = await userManager.GetUserAsync(context.User);
                    if (user != null &&
                        await userManager.IsInRoleAsync(user, "Admin") &&
                        !user.TwoFactorEnabled)
                    {
                        context.Response.Redirect("/Identity/Account/Manage/TwoFactorAuthentication");
                        return;
                    }
                }
            }
            await next(context);
        }
    }
}
