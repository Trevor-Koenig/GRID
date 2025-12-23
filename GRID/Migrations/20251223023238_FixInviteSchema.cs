using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRID.Migrations
{
    /// <inheritdoc />
    public partial class FixInviteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Invites",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Invites",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
