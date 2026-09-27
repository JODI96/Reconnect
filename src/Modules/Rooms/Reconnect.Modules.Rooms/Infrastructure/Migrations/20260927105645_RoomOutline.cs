using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.Rooms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomOutline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "anchor_latitude",
                schema: "rooms",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "anchor_longitude",
                schema: "rooms",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "anchor_yaw",
                schema: "rooms",
                table: "rooms",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outline",
                schema: "rooms",
                table: "rooms",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anchor_latitude",
                schema: "rooms",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "anchor_longitude",
                schema: "rooms",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "anchor_yaw",
                schema: "rooms",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "outline",
                schema: "rooms",
                table: "rooms");
        }
    }
}
