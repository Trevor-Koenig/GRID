using System.ComponentModel.DataAnnotations;

namespace GRID.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        [Required, MaxLength(256)]
        public string RoleName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Permission { get; set; } = string.Empty;
    }
}
