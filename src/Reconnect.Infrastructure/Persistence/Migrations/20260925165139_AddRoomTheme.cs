using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "theme",
                table: "rooms",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "cozy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "theme",
                table: "rooms");
        }
    }
}
