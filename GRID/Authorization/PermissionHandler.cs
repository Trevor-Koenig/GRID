using GRID.Services;
using Microsoft.AspNetCore.Authorization;

namespace GRID.Authorization
{
    public class PermissionHandler(PermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var roles = context.User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value);

            if (await permissionService.UserHasPermissionAsync(roles, requirement.Permission))
                context.Succeed(requirement);
        }
    }
}
