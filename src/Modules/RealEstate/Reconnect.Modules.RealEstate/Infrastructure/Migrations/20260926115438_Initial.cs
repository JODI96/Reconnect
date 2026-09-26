using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.RealEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "realestate");

            migrationBuilder.CreateTable(
                name: "offices",
                schema: "realestate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    price_rappen = table.Column<long>(type: "bigint", nullable: false),
                    area_square_meters = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_offices_building_id_floor",
                schema: "realestate",
                table: "offices",
                columns: new[] { "building_id", "floor" });

            migrationBuilder.CreateIndex(
                name: "ix_offices_owner_id",
                schema: "realestate",
                table: "offices",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offices",
                schema: "realestate");
        }
    }
}
