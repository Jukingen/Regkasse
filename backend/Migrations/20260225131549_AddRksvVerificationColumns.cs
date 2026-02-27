using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRksvVerificationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "TseSignatures",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsHeader",
                table: "TseSignatures",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsPayload",
                table: "TseSignatures",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsSignature",
                table: "TseSignatures",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "TseSignatures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureFormat",
                table: "TseSignatures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jws_header",
                table: "receipts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jws_payload",
                table: "receipts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jws_signature",
                table: "receipts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_format",
                table: "receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "payment_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "payment_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jws_header",
                table: "payment_details",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jws_payload",
                table: "payment_details",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jws_signature",
                table: "payment_details",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_format",
                table: "payment_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsHeader",
                table: "invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsPayload",
                table: "invoices",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsSignature",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureFormat",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "DailyClosings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsHeader",
                table: "DailyClosings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsPayload",
                table: "DailyClosings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JwsSignature",
                table: "DailyClosings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "DailyClosings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureFormat",
                table: "DailyClosings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "JwsHeader",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "JwsPayload",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "JwsSignature",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "SignatureFormat",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "jws_header",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "jws_payload",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "jws_signature",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "signature_format",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "jws_header",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "jws_payload",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "jws_signature",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "signature_format",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "JwsHeader",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "JwsPayload",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "JwsSignature",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SignatureFormat",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "JwsHeader",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "JwsPayload",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "JwsSignature",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "SignatureFormat",
                table: "DailyClosings");
        }
    }
}
