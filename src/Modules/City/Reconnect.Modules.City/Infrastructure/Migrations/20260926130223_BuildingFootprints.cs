using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Reconnect.Modules.City.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BuildingFootprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "city",
                table: "buildings",
                keyColumn: "id",
                keyValue: new Guid("0199a000-0000-7000-8000-000000000003"),
                column: "footprint",
                value: (NetTopologySuite.Geometries.Polygon)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POLYGON ((8.5172916 47.3858168, 8.5173161 47.3858427, 8.5174734 47.3859997, 8.5174691 47.3860325, 8.5174531 47.3861503, 8.5174915 47.3862611, 8.5175038 47.3862929, 8.5172469 47.3864032, 8.5172227 47.3863787, 8.5170682 47.3862219, 8.517071 47.3861908, 8.5170859 47.3860271, 8.5170304 47.3859049, 8.5170173 47.3858759, 8.5172916 47.3858168))"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "city",
                table: "buildings",
                keyColumn: "id",
                keyValue: new Guid("0199a000-0000-7000-8000-000000000003"),
                column: "footprint",
                value: null);
        }
    }
}
