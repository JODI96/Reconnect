using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.Profiles.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AvatarLook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "look",
                schema: "profiles",
                table: "profiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "look",
                schema: "profiles",
                table: "profiles");
        }
    }
}
