using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseSalesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_billing_audit_log_AspNetUsers_actor_user_id",
                table: "billing_audit_log");

            migrationBuilder.DropForeignKey(
                name: "FK_billing_audit_log_license_sales_license_sale_id",
                table: "billing_audit_log");

            migrationBuilder.DropForeignKey(
                name: "FK_billing_audit_log_tenants_tenant_id",
                table: "billing_audit_log");

            migrationBuilder.DropIndex(
                name: "idx_license_sales_license_key",
                table: "license_sales");

            migrationBuilder.DropIndex(
                name: "idx_billing_audit_log_license_sale_id",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "license_key",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "license_plan",
                table: "billing_audit_log");

            migrationBuilder.AddColumn<Guid>(
                name: "sale_id",
                table: "billing_audit_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE billing_audit_log
                SET sale_id = license_sale_id
                WHERE license_sale_id IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "license_sale_id",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "price_gross",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "price_net",
                table: "billing_audit_log");

            migrationBuilder.RenameColumn(
                name: "event_type",
                table: "billing_audit_log",
                newName: "action");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "billing_audit_log",
                newName: "timestamp_utc");

            migrationBuilder.RenameColumn(
                name: "actor_user_id",
                table: "billing_audit_log",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "IX_billing_audit_log_actor_user_id",
                table: "billing_audit_log",
                newName: "IX_billing_audit_log_user_id");

            migrationBuilder.RenameIndex(
                name: "idx_billing_audit_log_event_type",
                table: "billing_audit_log",
                newName: "idx_billing_audit_log_action");

            migrationBuilder.RenameIndex(
                name: "idx_billing_audit_log_created_at",
                table: "billing_audit_log",
                newName: "idx_billing_audit_log_timestamp_utc");

            migrationBuilder.Sql(
                """
                UPDATE billing_audit_log
                SET action = 'SALE_CREATED'
                WHERE action = 'license_sold';

                UPDATE billing_audit_log
                SET action = 'SALE_CANCELLED'
                WHERE action = 'license_cancelled';
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "activation_date_utc",
                table: "license_sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extended_by_user_id",
                table: "license_sales",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_extended_at_utc",
                table: "license_sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "billing_audit_log",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "details",
                table: "billing_audit_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "billing_audit_log",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "license_reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reminder_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reminder_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "expiry"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_reminders", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_reminders_license_sales_license_sale_id",
                        column: x => x.license_sale_id,
                        principalTable: "license_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_reminders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_license_key",
                table: "license_sales",
                column: "license_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_sales_extended_by_user_id",
                table: "license_sales",
                column: "extended_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_sale_id",
                table: "billing_audit_log",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_license_sale_id",
                table: "license_reminders",
                column: "license_sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_reminder_date",
                table: "license_reminders",
                column: "reminder_date_utc");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_status",
                table: "license_reminders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_tenant_id",
                table: "license_reminders",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_AspNetUsers_user_id",
                table: "billing_audit_log",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_license_sales_sale_id",
                table: "billing_audit_log",
                column: "sale_id",
                principalTable: "license_sales",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_tenants_tenant_id",
                table: "billing_audit_log",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_license_sales_AspNetUsers_extended_by_user_id",
                table: "license_sales",
                column: "extended_by_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_billing_audit_log_AspNetUsers_user_id",
                table: "billing_audit_log");

            migrationBuilder.DropForeignKey(
                name: "FK_billing_audit_log_license_sales_sale_id",
                table: "billing_audit_log");

            migrationBuilder.DropForeignKey(
                name: "FK_billing_audit_log_tenants_tenant_id",
                table: "billing_audit_log");

            migrationBuilder.DropForeignKey(
                name: "FK_license_sales_AspNetUsers_extended_by_user_id",
                table: "license_sales");

            migrationBuilder.DropTable(
                name: "license_reminders");

            migrationBuilder.DropIndex(
                name: "idx_license_sales_license_key",
                table: "license_sales");

            migrationBuilder.DropIndex(
                name: "IX_license_sales_extended_by_user_id",
                table: "license_sales");

            migrationBuilder.DropIndex(
                name: "idx_billing_audit_log_sale_id",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "activation_date_utc",
                table: "license_sales");

            migrationBuilder.DropColumn(
                name: "extended_by_user_id",
                table: "license_sales");

            migrationBuilder.DropColumn(
                name: "last_extended_at_utc",
                table: "license_sales");

            migrationBuilder.DropColumn(
                name: "details",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "billing_audit_log");

            migrationBuilder.DropColumn(
                name: "sale_id",
                table: "billing_audit_log");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "billing_audit_log",
                newName: "actor_user_id");

            migrationBuilder.RenameColumn(
                name: "timestamp_utc",
                table: "billing_audit_log",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "action",
                table: "billing_audit_log",
                newName: "event_type");

            migrationBuilder.RenameIndex(
                name: "IX_billing_audit_log_user_id",
                table: "billing_audit_log",
                newName: "IX_billing_audit_log_actor_user_id");

            migrationBuilder.RenameIndex(
                name: "idx_billing_audit_log_timestamp_utc",
                table: "billing_audit_log",
                newName: "idx_billing_audit_log_created_at");

            migrationBuilder.RenameIndex(
                name: "idx_billing_audit_log_action",
                table: "billing_audit_log",
                newName: "idx_billing_audit_log_event_type");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "billing_audit_log",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "billing_audit_log",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "billing_audit_log",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR");

            migrationBuilder.AddColumn<string>(
                name: "invoice_number",
                table: "billing_audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "license_key",
                table: "billing_audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "license_plan",
                table: "billing_audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "license_sale_id",
                table: "billing_audit_log",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "price_gross",
                table: "billing_audit_log",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "price_net",
                table: "billing_audit_log",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_license_key",
                table: "license_sales",
                column: "license_key");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_license_sale_id",
                table: "billing_audit_log",
                column: "license_sale_id");

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_AspNetUsers_actor_user_id",
                table: "billing_audit_log",
                column: "actor_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_license_sales_license_sale_id",
                table: "billing_audit_log",
                column: "license_sale_id",
                principalTable: "license_sales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_tenants_tenant_id",
                table: "billing_audit_log",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
