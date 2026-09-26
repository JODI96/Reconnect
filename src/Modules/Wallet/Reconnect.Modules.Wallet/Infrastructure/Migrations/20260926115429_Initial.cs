using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconnect.Modules.Wallet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wallet");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "wallet",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_rappen = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.user_id);
                    table.CheckConstraint("ck_accounts_balance_not_negative", "balance_rappen >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ledger",
                schema: "wallet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_rappen = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_accounts_user_id",
                        column: x => x.user_id,
                        principalSchema: "wallet",
                        principalTable: "accounts",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_user_id_created_at",
                schema: "wallet",
                table: "ledger",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger",
                schema: "wallet");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "wallet");
        }
    }
}
