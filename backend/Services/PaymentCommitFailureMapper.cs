using KasseAPI_Final.DTOs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Pure mapping from payment-commit database exceptions to <see cref="PaymentResult"/>.
    /// No I/O. Fiscal transactions keep calling the same predicates.
    /// </summary>
    public static class PaymentCommitFailureMapper
    {
        /// <summary>
        /// Returns the standard PaymentResult for daily allowance conflict (concurrency or unique-index race).
        /// Keeps contract stable for mobile POS.
        /// </summary>
        public static PaymentResult ToDailyAllowanceConflictResult()
        {
            return new PaymentResult
            {
                Success = false,
                Message = "Daily allowance was used at another terminal. Please try again.",
                DiagnosticCode = "BENEFIT_DAILY_ALLOWANCE_CONFLICT",
                Errors = { "Benefit daily allowance concurrency conflict" }
            };
        }

        /// <summary>
        /// True when the exception is a PostgreSQL unique constraint violation (23505) on the idempotency_key column.
        /// Used to return existing payment on concurrent duplicate key insert.
        /// </summary>
        public static bool IsIdempotencyKeyViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505" &&
                    (pg.ConstraintName?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) ?? false))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Unique violation on <c>voucher_ledger_entries.idempotency_key</c> (e.g. index IX_voucher_ledger_entries_idempotency_key).
        /// </summary>
        public static bool IsVoucherLedgerIdempotencyConstraintViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505" &&
                    (pg.ConstraintName?.Contains("voucher_ledger_entries_idempotency", StringComparison.OrdinalIgnoreCase) ?? false))
                    return true;
            }
            return false;
        }

        /// <summary>Sprint 6: True when the exception is a unique constraint violation on cancel_idempotency_key.</summary>
        public static bool IsCancelIdempotencyKeyViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505")
                {
                    var name = pg.ConstraintName ?? "";
                    if (name.Contains("cancel", StringComparison.OrdinalIgnoreCase) && name.Contains("idempotency", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public static bool IsStornoUniqueViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505")
                {
                    var name = pg.ConstraintName ?? "";
                    if (name.Contains("one_storno", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True when the exception is a PostgreSQL unique constraint violation (23505) on BenefitDailyUsage
        /// (CustomerId, BenefitDefinitionId, UsageDate) create-if-missing race. Excludes idempotency_key violations.
        /// </summary>
        public static bool IsBenefitDailyUsageConflict(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505" &&
                    (pg.ConstraintName?.Contains("benefit", StringComparison.OrdinalIgnoreCase) ?? false))
                    return true;
            }
            return false;
        }

        /// <summary>Walks inner exceptions for <see cref="PostgresException"/> (Npgsql may nest it).</summary>
        public static bool TryGetPostgresException(Exception ex, out PostgresException pgEx)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg)
                {
                    pgEx = pg;
                    return true;
                }
            }

            pgEx = null!;
            return false;
        }

        /// <summary>
        /// Maps PostgreSQL errors from payment fiscal transaction SaveChanges to a deterministic <see cref="PaymentResult"/>.
        /// </summary>
        public static bool TryMapPaymentCommitPostgresException(PostgresException pgEx, out PaymentResult result)
        {
            result = null!;
            var constraint = pgEx.ConstraintName ?? "";

            if (pgEx.SqlState == "23505")
            {
                if (constraint.Contains("voucher_ledger_entries_idempotency", StringComparison.OrdinalIgnoreCase))
                {
                    const string msg = "Voucher already used in this transaction. Please retry with a new idempotency key.";
                    result = new PaymentResult
                    {
                        Success = false,
                        Message = msg,
                        Errors = { msg },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_LEDGER_IDEMPOTENCY_CONFLICT"
                    };
                    return true;
                }

                if (constraint.Contains("payment_details_idempotency", StringComparison.OrdinalIgnoreCase)
                    || (constraint.Contains("payment_details", StringComparison.OrdinalIgnoreCase)
                        && constraint.Contains("idempotency", StringComparison.OrdinalIgnoreCase)))
                {
                    const string msg = "Duplicate transaction detected. Please retry.";
                    result = new PaymentResult
                    {
                        Success = false,
                        Message = msg,
                        Errors = { msg },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "DUPLICATE_IDEMPOTENCY_KEY"
                    };
                    return true;
                }

                if (constraint.Contains("receipt_number", StringComparison.OrdinalIgnoreCase))
                {
                    const string msg = "Receipt number conflict. Please contact support.";
                    result = new PaymentResult
                    {
                        Success = false,
                        Message = msg,
                        Errors = { msg },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "RECEIPT_NUMBER_CONFLICT"
                    };
                    return true;
                }

                var uniqueMsg = $"Database constraint violation: {pgEx.ConstraintName}";
                result = new PaymentResult
                {
                    Success = false,
                    Message = uniqueMsg,
                    Errors = { uniqueMsg },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "UNIQUE_VIOLATION"
                };
                return true;
            }

            if (pgEx.SqlState == "23503")
            {
                const string msg = "Referenced record not found. Please check voucher or customer data.";
                result = new PaymentResult
                {
                    Success = false,
                    Message = msg,
                    Errors = { msg },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "FOREIGN_KEY_VIOLATION"
                };
                return true;
            }

            if (pgEx.SqlState == "23514")
            {
                const string msg = "Data validation failed. Please check voucher amount.";
                result = new PaymentResult
                {
                    Success = false,
                    Message = msg,
                    Errors = { msg },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "CHECK_VIOLATION"
                };
                return true;
            }

            return false;
        }
    }
}
