using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.Social.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "social");

            migrationBuilder.CreateTable(
                name: "likes",
                schema: "social",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_likes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                schema: "social",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user1id = table.Column<Guid>(type: "uuid", nullable: false),
                    user2id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "social",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_messages_matches_match_id",
                        column: x => x.match_id,
                        principalSchema: "social",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_likes_from_user_id_to_user_id",
                schema: "social",
                table: "likes",
                columns: new[] { "from_user_id", "to_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_likes_to_user_id",
                schema: "social",
                table: "likes",
                column: "to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_user1id_user2id",
                schema: "social",
                table: "matches",
                columns: new[] { "user1id", "user2id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_user2id",
                schema: "social",
                table: "matches",
                column: "user2id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_match_id_sent_at",
                schema: "social",
                table: "messages",
                columns: new[] { "match_id", "sent_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "likes",
                schema: "social");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "social");

            migrationBuilder.DropTable(
                name: "matches",
                schema: "social");
        }
    }
}
