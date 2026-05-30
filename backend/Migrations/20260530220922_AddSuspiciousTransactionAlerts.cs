using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspiciousTransactionAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS suspicious_transaction_alerts (
                    id uuid NOT NULL,
                    tenant_id uuid NOT NULL,
                    alert_type integer NOT NULL,
                    severity integer NOT NULL,
                    status integer NOT NULL,
                    payment_id uuid,
                    customer_id uuid,
                    user_id character varying(450),
                    message text NOT NULL,
                    suggested_action text,
                    details_json jsonb,
                    dedup_key character varying(120) NOT NULL,
                    detected_at_utc timestamp with time zone NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone,
                    created_by character varying(450),
                    updated_by character varying(450),
                    is_active boolean NOT NULL,
                    CONSTRAINT "PK_suspicious_transaction_alerts" PRIMARY KEY (id)
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS user_permission_overrides (
                    id uuid NOT NULL,
                    user_id character varying(450) NOT NULL,
                    tenant_id uuid,
                    permission character varying(128) NOT NULL,
                    is_granted boolean NOT NULL,
                    reason character varying(500),
                    created_at timestamp with time zone NOT NULL,
                    created_by_user_id character varying(450),
                    expires_at timestamp with time zone,
                    CONSTRAINT "PK_user_permission_overrides" PRIMARY KEY (id),
                    CONSTRAINT "FK_user_permission_overrides_AspNetUsers_user_id" FOREIGN KEY (user_id)
                        REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_suspicious_transaction_alerts_tenant_id"
                    ON suspicious_transaction_alerts (tenant_id);
                CREATE INDEX IF NOT EXISTS "IX_suspicious_transaction_alerts_tenant_id_dedup_key_detected_~"
                    ON suspicious_transaction_alerts (tenant_id, dedup_key, detected_at_utc);
                CREATE INDEX IF NOT EXISTS "IX_suspicious_transaction_alerts_tenant_id_status_detected_at_~"
                    ON suspicious_transaction_alerts (tenant_id, status, detected_at_utc);
                CREATE INDEX IF NOT EXISTS "IX_user_permission_overrides_tenant_id"
                    ON user_permission_overrides (tenant_id);
                CREATE INDEX IF NOT EXISTS "IX_user_permission_overrides_user_id"
                    ON user_permission_overrides (user_id);
                CREATE INDEX IF NOT EXISTS "IX_user_permission_overrides_user_id_tenant_id_permission"
                    ON user_permission_overrides (user_id, tenant_id, permission);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "suspicious_transaction_alerts");
            migrationBuilder.DropTable(name: "user_permission_overrides");
        }
    }
}
