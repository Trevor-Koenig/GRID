using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRID.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "AuditLogs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "AuditLogs");
        }
    }
}
