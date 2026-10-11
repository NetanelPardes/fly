using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Airport.Api.Migrations
{
    /// <inheritdoc />
    public partial class RespecAirportDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "varchar(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Users",
                type: "varchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "BaggageAllowanceKg",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BookingDate",
                table: "Tickets",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<string>(
                name: "PassengerEmail",
                table: "Tickets",
                type: "varchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PassengerNationalId",
                table: "Tickets",
                type: "varchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AircraftCode",
                table: "Flights",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Flights",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BoardingGate",
                table: "Flights",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DestinationAirportCode",
                table: "Flights",
                type: "varchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OriginAirportCode",
                table: "Flights",
                type: "varchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PassengerCount",
                table: "Flights",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Manufacturer = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Model = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeatCount = table.Column<int>(type: "int", nullable: false),
                    ManufactureYear = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.Code);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AirportLocations",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Country = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportLocations", x => x.Code);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE `Users`
                SET
                    `NationalId` = LPAD(CAST(100000000 + `Id` * 7919 AS CHAR), 9, '0'),
                    `FirstName` = CASE
                        WHEN LOCATE(' ', TRIM(`FullName`)) > 0 THEN SUBSTRING_INDEX(TRIM(`FullName`), ' ', 1)
                        ELSE TRIM(`FullName`)
                    END,
                    `LastName` = CASE
                        WHEN LOCATE(' ', TRIM(`FullName`)) > 0 THEN SUBSTRING(TRIM(`FullName`), LOCATE(' ', TRIM(`FullName`)) + 1)
                        ELSE 'User'
                    END,
                    `Gender` = 'm',
                    `Address` = CONCAT('Airport Avenue ', `Id`),
                    `PhoneNumber` = CONCAT('050', LPAD(CAST(`Id` AS CHAR), 7, '0'))
                WHERE `NationalId` = '' OR `PhoneNumber` = '';
                """);

            migrationBuilder.Sql("""
                UPDATE `Tickets` ticket
                JOIN `Users` user ON user.`Id` = ticket.`PassengerId`
                SET
                    ticket.`PassengerNationalId` = user.`NationalId`,
                    ticket.`PassengerEmail` = user.`Email`
                WHERE ticket.`PassengerNationalId` = '' OR ticket.`PassengerEmail` = '';
                """);

            migrationBuilder.Sql("""
                UPDATE `Flights`
                SET `BasePrice` = 180
                WHERE `BasePrice` = 0;
                """);

            migrationBuilder.UpdateData(
                table: "Flights",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AircraftCode", "BasePrice", "BoardingGate", "DestinationAirportCode", "OriginAirportCode", "PassengerCount" },
                values: new object[] { "DEMO-737", 180m, "A1", "LON", "TLV", 0 });

            migrationBuilder.UpdateData(
                table: "Flights",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AircraftCode", "BasePrice", "BoardingGate", "DestinationAirportCode", "OriginAirportCode", "PassengerCount" },
                values: new object[] { "DEMO-320", 180m, "A2", "TLV", "PAR", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NationalId",
                table: "Users",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BookingDate",
                table: "Tickets",
                column: "BookingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PassengerEmail",
                table: "Tickets",
                column: "PassengerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PassengerNationalId",
                table: "Tickets",
                column: "PassengerNationalId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftCode_DepartureTime",
                table: "Flights",
                columns: new[] { "AircraftCode", "DepartureTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_OriginAirportCode_DepartureTime",
                table: "Flights",
                columns: new[] { "OriginAirportCode", "DepartureTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_OriginAirportCode_DestinationAirportCode_DepartureTi~",
                table: "Flights",
                columns: new[] { "OriginAirportCode", "DestinationAirportCode", "DepartureTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_Manufacturer",
                table: "Aircraft",
                column: "Manufacturer");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_Model",
                table: "Aircraft",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_AirportLocations_City_Country",
                table: "AirportLocations",
                columns: new[] { "City", "Country" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aircraft");

            migrationBuilder.DropTable(
                name: "AirportLocations");

            migrationBuilder.DropIndex(
                name: "IX_Users_NationalId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_BookingDate",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_PassengerEmail",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_PassengerNationalId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Flights_AircraftCode_DepartureTime",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_OriginAirportCode_DepartureTime",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_OriginAirportCode_DestinationAirportCode_DepartureTi~",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BaggageAllowanceKg",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "BookingDate",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PassengerEmail",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PassengerNationalId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AircraftCode",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "BoardingGate",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DestinationAirportCode",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "OriginAirportCode",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "PassengerCount",
                table: "Flights");
        }
    }
}
