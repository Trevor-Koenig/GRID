using GRID.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRID.Migrations
{
    /// <summary>
    /// Data-only migration: role permissions are seeded only into an empty table, so existing
    /// installs need admin.backups granted to the Admin role explicitly. Fresh installs (empty
    /// table) are left to the startup seeding.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261006000001_GrantBackupPermissionToAdmin")]
    public partial class GrantBackupPermissionToAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "RolePermissions" ("RoleName", "Permission")
                SELECT 'Admin', 'admin.backups'
                WHERE EXISTS (SELECT 1 FROM "RolePermissions" WHERE "RoleName" = 'Admin' AND "Permission" = 'admin.access')
                  AND NOT EXISTS (SELECT 1 FROM "RolePermissions" WHERE "RoleName" = 'Admin' AND "Permission" = 'admin.backups');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "RolePermissions" WHERE "Permission" = 'admin.backups';""");
        }
    }
}
