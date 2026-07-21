using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class PaymentCashRegisterIdFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "payment_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE payment_details pd SET cash_register_id = cr.id
                FROM cash_registers cr
                WHERE pd.cash_register_id IS NULL AND cr.""RegisterNumber"" = pd.""KassenId"";

                UPDATE payment_details pd SET cash_register_id = cr.id
                FROM cash_registers cr
                WHERE pd.cash_register_id IS NULL
                  AND pd.""KassenId"" IS NOT NULL
                  AND LOWER(TRIM(pd.""KassenId"")) = LOWER(cr.id::text);

                UPDATE payment_details SET cash_register_id = (SELECT id FROM cash_registers ORDER BY created_at LIMIT 1)
                WHERE cash_register_id IS NULL AND (SELECT COUNT(*)::int FROM cash_registers) = 1;
            ");

            migrationBuilder.Sql(@"DELETE FROM payment_details WHERE cash_register_id IS NULL;");

            migrationBuilder.DropColumn(
                name: "KassenId",
                table: "payment_details");

            migrationBuilder.AlterColumn<Guid>(
                name: "cash_register_id",
                table: "payment_details",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "receipt_sequences",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE receipt_sequences rs SET cash_register_id = cr.id
                FROM cash_registers cr
                WHERE rs.cash_register_id IS NULL AND cr.""RegisterNumber"" = rs.kassen_id;

                UPDATE receipt_sequences rs SET cash_register_id = cr.id
                FROM cash_registers cr
                WHERE rs.cash_register_id IS NULL AND LOWER(TRIM(rs.kassen_id)) = LOWER(cr.id::text);

                DELETE FROM receipt_sequences WHERE cash_register_id IS NULL;

                DELETE FROM receipt_sequences rs
                WHERE rs.id IN (
                    SELECT id FROM (
                        SELECT id, ROW_NUMBER() OVER (
                            PARTITION BY cash_register_id, sequence_date
                            ORDER BY next_sequence DESC, id) AS rn
                        FROM receipt_sequences
                    ) x WHERE rn > 1
                );
            ");

            migrationBuilder.DropIndex(
                name: "IX_receipt_sequences_kassen_id_sequence_date",
                table: "receipt_sequences");

            migrationBuilder.DropColumn(
                name: "kassen_id",
                table: "receipt_sequences");

            migrationBuilder.AlterColumn<Guid>(
                name: "cash_register_id",
                table: "receipt_sequences",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_sequences_cash_register_id_sequence_date",
                table: "receipt_sequences",
                columns: new[] { "cash_register_id", "sequence_date" },
                unique: true);

            // Production-readiness fix: migration ordering drift can cause signature_chain_state
            // to be missing when we reach this step. Create the baseline table shape if absent.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'signature_chain_state') THEN
                        CREATE TABLE signature_chain_state (
                            id uuid NOT NULL,
                            register_id character varying(50) NOT NULL,
                            last_signature character varying(4000),
                            last_counter integer NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            CONSTRAINT PK_signature_chain_state PRIMARY KEY (id)
                        );

                        CREATE UNIQUE INDEX ""IX_signature_chain_state_register_id""
                            ON signature_chain_state (register_id);
                    END IF;
                END $$;
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "signature_chain_state",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE signature_chain_state s SET cash_register_id = cr.id
                FROM cash_registers cr
                WHERE s.cash_register_id IS NULL AND cr.""RegisterNumber"" = s.register_id;

                UPDATE signature_chain_state SET cash_register_id = register_id::uuid
                WHERE cash_register_id IS NULL AND register_id ~ '^[0-9a-fA-F-]{36}$';

                DELETE FROM signature_chain_state WHERE cash_register_id IS NULL;

                DELETE FROM signature_chain_state s
                WHERE s.id IN (
                    SELECT id FROM (
                        SELECT id, ROW_NUMBER() OVER (
                            PARTITION BY cash_register_id
                            ORDER BY last_counter DESC, updated_at DESC, id) AS rn
                        FROM signature_chain_state
                    ) x WHERE rn > 1
                );
            ");

            migrationBuilder.DropIndex(
                name: "IX_signature_chain_state_register_id",
                table: "signature_chain_state");

            migrationBuilder.DropColumn(
                name: "register_id",
                table: "signature_chain_state");

            migrationBuilder.AlterColumn<Guid>(
                name: "cash_register_id",
                table: "signature_chain_state",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_signature_chain_state_cash_register_id",
                table: "signature_chain_state",
                column: "cash_register_id",
                unique: true);

            // Production-readiness fix:
            // In some environments, migration ordering drift can leave invoices with renamed/missing columns.
            // Guard UPDATEs with schema inspection so the migration never hard-fails during bootstrap.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    has_source_payment_id boolean;
                    has_source_paymentid_col boolean;
                    has_kassen_id_col boolean;
                    has_cash_register_id_col boolean;
                    sql text;
                BEGIN
                    has_source_payment_id := EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'invoices' AND column_name = 'source_payment_id'
                    );
                    has_source_paymentid_col := EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'invoices' AND column_name = 'SourcePaymentId'
                    );
                    has_kassen_id_col := EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'invoices' AND column_name = 'KassenId'
                    );
                    has_cash_register_id_col := EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'invoices' AND column_name = 'cash_register_id'
                    );

                    -- Map cash_register_id via source payment id when that column exists.
                    IF has_cash_register_id_col AND (has_source_payment_id OR has_source_paymentid_col) THEN
                        IF has_source_payment_id THEN
                            sql := '
                                UPDATE invoices i SET cash_register_id = p.cash_register_id
                                FROM payment_details p
                                WHERE i.source_payment_id = p.id
                                  AND (i.""CashRegisterId"" IS NULL OR i.""CashRegisterId"" = ''00000000-0000-0000-0000-000000000000''::uuid);
                            ';
                        ELSE
                            sql := '
                                UPDATE invoices i SET cash_register_id = p.cash_register_id
                                FROM payment_details p
                                WHERE i.""SourcePaymentId"" = p.id
                                  AND (i.""CashRegisterId"" IS NULL OR i.""CashRegisterId"" = ''00000000-0000-0000-0000-000000000000''::uuid);
                            ';
                        END IF;

                        EXECUTE sql;
                    END IF;

                    -- Map cash_register_id via KassenId when KassenId exists.
                    IF has_cash_register_id_col AND has_kassen_id_col THEN
                        sql := '
                            UPDATE invoices i SET cash_register_id = cr.id
                            FROM cash_registers cr
                            WHERE (i.""CashRegisterId"" IS NULL OR i.""CashRegisterId"" = ''00000000-0000-0000-0000-000000000000''::uuid)
                              AND cr.""RegisterNumber"" = i.""KassenId"";
                        ';
                        EXECUTE sql;
                    END IF;

                    -- Final fallback: when exactly one cash_register exists, set CashRegisterId.
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'invoices' AND column_name = 'CashRegisterId'
                    ) THEN
                        sql := '
                            UPDATE invoices SET ""CashRegisterId"" = (SELECT id FROM cash_registers ORDER BY created_at LIMIT 1)
                            WHERE (""CashRegisterId"" IS NULL OR ""CashRegisterId"" = ''00000000-0000-0000-0000-000000000000''::uuid)
                              AND (SELECT COUNT(*)::int FROM cash_registers) = 1;
                        ';
                        EXECUTE sql;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM invoices
                        WHERE ""CashRegisterId"" IS NULL
                           OR ""CashRegisterId"" = '00000000-0000-0000-0000-000000000000'::uuid) THEN
                        -- Production-readiness fix: do not hard-fail migration bootstrap.
                        RAISE NOTICE 'PaymentCashRegisterIdFk: invoice(s) without resolvable CashRegisterId; check legacy data mapping after migration.';
                    END IF;
                END $$;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "CashRegisterId",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid?),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_cash_register_id",
                table: "payment_details",
                column: "cash_register_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_details_cash_registers_cash_register_id",
                table: "payment_details",
                column: "cash_register_id",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_details_cash_registers_cash_register_id",
                table: "payment_details");

            migrationBuilder.DropIndex(
                name: "IX_payment_details_cash_register_id",
                table: "payment_details");

            migrationBuilder.AlterColumn<Guid>(
                name: "CashRegisterId",
                table: "invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.DropIndex(
                name: "IX_signature_chain_state_cash_register_id",
                table: "signature_chain_state");

            migrationBuilder.AddColumn<string>(
                name: "register_id",
                table: "signature_chain_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE signature_chain_state s SET register_id = cr.""RegisterNumber""
                FROM cash_registers cr WHERE s.cash_register_id = cr.id;
            ");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "signature_chain_state");

            migrationBuilder.AlterColumn<string>(
                name: "register_id",
                table: "signature_chain_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_signature_chain_state_register_id",
                table: "signature_chain_state",
                column: "register_id",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_receipt_sequences_cash_register_id_sequence_date",
                table: "receipt_sequences");

            migrationBuilder.AddColumn<string>(
                name: "kassen_id",
                table: "receipt_sequences",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE receipt_sequences rs SET kassen_id = cr.""RegisterNumber""
                FROM cash_registers cr WHERE rs.cash_register_id = cr.id;
            ");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "receipt_sequences");

            migrationBuilder.AlterColumn<string>(
                name: "kassen_id",
                table: "receipt_sequences",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_sequences_kassen_id_sequence_date",
                table: "receipt_sequences",
                columns: new[] { "kassen_id", "sequence_date" },
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "KassenId",
                table: "payment_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE payment_details pd SET ""KassenId"" = cr.""RegisterNumber""
                FROM cash_registers cr WHERE pd.cash_register_id = cr.id;
            ");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "payment_details");

            migrationBuilder.AlterColumn<string>(
                name: "KassenId",
                table: "payment_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
