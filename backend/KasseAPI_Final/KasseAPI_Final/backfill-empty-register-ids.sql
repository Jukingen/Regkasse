-- Backfill script to resolve missing CashRegisterId for Invoices created with Guid.Empty (migrated to NULL)
-- or legacy PaymentDetails where KassenId didn't link directly to the CashRegisters.Id
-- This queries the CashRegisters table by RegisterNumber or implicit CAST to map missing Invoice foreign keys back.

DO $$
DECLARE
    updated_count INTEGER;
BEGIN
    UPDATE invoices
    SET cash_register_id = cr.id
    FROM cash_registers cr
    WHERE invoices.KassenId = cr.RegisterNumber
      AND invoices.cash_register_id IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;
    RAISE NOTICE 'Backfilled % invoices using CashRegister.RegisterNumber.', updated_count;

    -- Safety fallback if KassenId was explicitly saved as the UUID string
    UPDATE invoices
    SET cash_register_id = CAST(KassenId AS uuid)
    WHERE cash_register_id IS NULL
      AND KassenId ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';
      
    GET DIAGNOSTICS updated_count = ROW_COUNT;
    RAISE NOTICE 'Backfilled % invoices using CashRegister UUID casts.', updated_count;
END $$;
