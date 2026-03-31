using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRID.Migrations
{
    /// <inheritdoc />
    public partial class AddIpAndStatusToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HttpStatus",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            // Backfill from existing "IP: x.x.x.x | Status: 200" Details format
            migrationBuilder.Sql("""
                UPDATE "AuditLogs"
                SET
                    "IpAddress" = TRIM(SPLIT_PART(SPLIT_PART("Details", 'IP: ', 2), ' |', 1)),
                    "HttpStatus" = CAST(NULLIF(TRIM(SPLIT_PART("Details", 'Status: ', 2)), '') AS INTEGER)
                WHERE "Action" = 'PageView'
                  AND "Details" LIKE 'IP: %';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "HttpStatus",
                table: "AuditLogs");
        }
    }
}
