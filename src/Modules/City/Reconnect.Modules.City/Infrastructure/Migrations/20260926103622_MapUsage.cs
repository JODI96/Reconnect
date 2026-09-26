using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.City.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MapUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_usage",
                schema: "city",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    is_premium = table.Column<bool>(type: "boolean", nullable: false),
                    google_sessions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_usage", x => new { x.user_id, x.month });
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_usage_month_is_premium",
                schema: "city",
                table: "map_usage",
                columns: new[] { "month", "is_premium" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_usage",
                schema: "city");
        }
    }
}
