using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace GRID.Authorization
{
    /// <summary>
    /// Returns 404 for any authorization failure on /Admin routes, regardless of whether
    /// the user is unauthenticated or authenticated-but-unauthorized. This hides the
    /// existence of the admin surface entirely.
    /// </summary>
    public class AdminNotFoundHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            if (!authorizeResult.Succeeded &&
                context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await _default.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
