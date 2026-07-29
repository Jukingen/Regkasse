using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTseDeviceSignaturkarteProgramCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SignaturkarteProgramCompliantAtUtc",
                table: "TseDevices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignaturkarteProgramCompliantBy",
                table: "TseDevices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignaturkarteProgramNote",
                table: "TseDevices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignaturkarteProgramCompliantAtUtc",
                table: "TseDevices");

            migrationBuilder.DropColumn(
                name: "SignaturkarteProgramCompliantBy",
                table: "TseDevices");

            migrationBuilder.DropColumn(
                name: "SignaturkarteProgramNote",
                table: "TseDevices");
        }
    }
}
