using GRID.Data;
using GRID.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace GRID.Services
{
    public class PermissionService(IServiceScopeFactory scopeFactory)
    {
        private ConcurrentDictionary<string, HashSet<string>>? _cache;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private async Task<ConcurrentDictionary<string, HashSet<string>>> GetCacheAsync()
        {
            if (_cache != null) return _cache;

            await _lock.WaitAsync();
            try
            {
                if (_cache != null) return _cache;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var all = await db.RolePermissions.ToListAsync();
                var dict = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var rp in all)
                {
                    dict.GetOrAdd(rp.RoleName, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                        .Add(rp.Permission);
                }

                _cache = dict;
                return _cache;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> RoleHasPermissionAsync(string roleName, string permission)
        {
            var cache = await GetCacheAsync();
            return cache.TryGetValue(roleName, out var perms) && perms.Contains(permission);
        }

        public async Task<bool> UserHasPermissionAsync(IEnumerable<string> userRoles, string permission)
        {
            var cache = await GetCacheAsync();
            foreach (var role in userRoles)
            {
                if (cache.TryGetValue(role, out var perms) && perms.Contains(permission))
                    return true;
            }
            return false;
        }

        public async Task<HashSet<string>> GetPermissionsForRoleAsync(string roleName)
        {
            var cache = await GetCacheAsync();
            return cache.TryGetValue(roleName, out var perms)
                ? new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase)
                : [];
        }

        public void InvalidateCache() => _cache = null;
    }
}
