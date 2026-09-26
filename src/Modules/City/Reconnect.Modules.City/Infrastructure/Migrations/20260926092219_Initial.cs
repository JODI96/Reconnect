using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Reconnect.Modules.City.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "city");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "city",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    location = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    footprint = table.Column<Polygon>(type: "geography (polygon, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildings", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "city",
                table: "buildings",
                columns: new[] { "id", "address", "footprint", "location", "name" },
                values: new object[,]
                {
                    { new Guid("0199a000-0000-7000-8000-000000000001"), "Bahnhofplatz 1, 8001 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54018 47.37785)"), "Zürich HB" },
                    { new Guid("0199a000-0000-7000-8000-000000000002"), "Grossmünsterplatz, 8001 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54411 47.37011)"), "Grossmünster" },
                    { new Guid("0199a000-0000-7000-8000-000000000003"), "Hardstrasse 201, 8005 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.51733 47.38622)"), "Prime Tower" },
                    { new Guid("0199a000-0000-7000-8000-000000000004"), "Rämistrasse 101, 8092 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54798 47.37635)"), "ETH Hauptgebäude" },
                    { new Guid("0199a000-0000-7000-8000-000000000005"), "Falkenstrasse 1, 8008 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54671 47.3649)"), "Opernhaus Zürich" },
                    { new Guid("0199a000-0000-7000-8000-000000000006"), "Museumstrasse 2, 8001 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54063 47.37926)"), "Landesmuseum" },
                    { new Guid("0199a000-0000-7000-8000-000000000007"), "Heimplatz 1, 8001 Zürich", null, (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (8.54826 47.37036)"), "Kunsthaus Zürich" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_buildings_location",
                schema: "city",
                table: "buildings",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buildings",
                schema: "city");
        }
    }
}
