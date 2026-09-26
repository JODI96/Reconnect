using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.Rooms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TowerFloors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "capacity",
                schema: "rooms",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<int>(
                name: "floor",
                schema: "rooms",
                table: "rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_rooms_building_id_floor",
                schema: "rooms",
                table: "rooms",
                columns: new[] { "building_id", "floor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rooms_building_id_floor",
                schema: "rooms",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "capacity",
                schema: "rooms",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "floor",
                schema: "rooms",
                table: "rooms");
        }
    }
}
