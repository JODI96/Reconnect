using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Reconnect.Modules.City.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeebadUtoquai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "city",
                table: "buildings",
                columns: new[] { "id", "address", "footprint", "location", "name" },
                values: new object[] { new Guid("0199a000-0000-7000-8000-000000000008"), "Utoquai 49, 8008 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54895 47.36271)"), "Seebad Utoquai" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "city",
                table: "buildings",
                keyColumn: "id",
                keyValue: new Guid("0199a000-0000-7000-8000-000000000008"));
        }
    }
}
