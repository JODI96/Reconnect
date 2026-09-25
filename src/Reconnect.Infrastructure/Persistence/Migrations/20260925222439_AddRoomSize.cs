using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "depth",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "width",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "depth",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "width",
                table: "rooms");
        }
    }
}
