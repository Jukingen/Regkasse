'use client';

/**
 * RKSV operations hub entry. PDF receipt reprint for RKSV special receipts (where a payment id exists)
 * is available under `/rksv/sonderbelege` — see table "Bestehende Sonderbelege" / "Zuletzt erstellte Sonderbelege", column Aktionen.
 */
import { RksvOperationsDashboard } from '@/features/rksv-operations/components/RksvOperationsDashboard';

export default function RksvOperationsPage() {
  return <RksvOperationsDashboard />;
}
