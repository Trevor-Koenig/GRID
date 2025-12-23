
namespace GRID.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.Security.Cryptography;

    public class Invite
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string? Role { get; set; }
        public bool IsSingleUse { get; set; } = true;
        public int? MaxUses { get; set; } = 1;
        public int CurrentUses { get; set; } = 0;
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public List<InviteUsage> Usages { get; set; } = [];

        [Timestamp]
        public byte[] RowVersion { get; set; }

        public Invite()
        {
            RowVersion = GenerateRandomRowVersion();
        }

        public static byte[] GenerateRandomRowVersion(int size = 8)
        {
            var bytes = new byte[size];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }
    }
}