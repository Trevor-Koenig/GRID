using Microsoft.AspNetCore.Authorization;

namespace GRID.Authorization
{
    public class PermissionRequirement(string permission) : IAuthorizationRequirement
    {
        public string Permission { get; } = permission;
    }
}
