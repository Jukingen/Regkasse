using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>RKSV/DSGVO: Deaktivasyon audit alanları (kim, ne zaman, neden).</summary>
    public partial class AddUserDeactivationAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deactivated_at",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deactivated_by",
                table: "AspNetUsers",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deactivation_reason",
                table: "AspNetUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deactivated_at",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "deactivated_by",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "deactivation_reason",
                table: "AspNetUsers");
        }
    }
}
