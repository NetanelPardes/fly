using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Airport.Api.Migrations
{
    /// <inheritdoc />
    public partial class RouteBookingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RouteBookingReference",
                table: "Tickets",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RouteLegCount",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RouteLegIndex",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RouteBookingReference",
                table: "Tickets",
                column: "RouteBookingReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_RouteBookingReference",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RouteBookingReference",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RouteLegCount",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RouteLegIndex",
                table: "Tickets");
        }
    }
}
