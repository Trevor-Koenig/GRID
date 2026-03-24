using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRID.Migrations
{
    /// <inheritdoc />
    public partial class FixInviteCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MaxUses",
                table: "Invites",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Invites",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invites_Uses",
                table: "Invites",
                sql: "\"MaxUses\" IS NULL OR \"CurrentUses\" <= \"MaxUses\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Invites_Uses",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Invites");

            migrationBuilder.AlterColumn<int>(
                name: "MaxUses",
                table: "Invites",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
