using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRID.Migrations
{
    /// <inheritdoc />
    public partial class SplitHomePageFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowInHero",
                table: "ServiceLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInServices",
                table: "ServiceLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Migrate existing data: carry ShowOnHomePage into both new flags
            migrationBuilder.Sql(
                "UPDATE \"ServiceLinks\" SET \"ShowInHero\" = \"ShowOnHomePage\", \"ShowInServices\" = \"ShowOnHomePage\"");

            migrationBuilder.DropColumn(
                name: "ShowOnHomePage",
                table: "ServiceLinks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnHomePage",
                table: "ServiceLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"ServiceLinks\" SET \"ShowOnHomePage\" = (\"ShowInHero\" OR \"ShowInServices\")");

            migrationBuilder.DropColumn(
                name: "ShowInHero",
                table: "ServiceLinks");

            migrationBuilder.DropColumn(
                name: "ShowInServices",
                table: "ServiceLinks");
        }
    }
}
