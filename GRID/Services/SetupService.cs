using GRID.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace GRID.Services
{
    /// <summary>
    /// Fresh-install state. While no accounts exist, a backup can be restored from /Setup/Restore
    /// by anyone holding the setup code, which is generated per process and only written to
    /// the container log — restoring runs arbitrary SQL, so reaching the page isn't enough.
    /// </summary>
    public class SetupService
    {
        public string SetupCode { get; } = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 12);

        public static async Task<bool> IsFreshInstallAsync(ApplicationDbContext db) => !await db.Users.AnyAsync();

        public bool IsValidCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var normalized = code.Trim().Replace("-", "").ToUpperInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(normalized), Encoding.ASCII.GetBytes(SetupCode));
        }

        /// <summary>The code as shown to people, e.g. ABCD-EFGH-JKLM.</summary>
        public string DisplayCode => string.Join('-', SetupCode.Chunk(4).Select(c => new string(c)));
    }
}
