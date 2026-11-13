/**
 * Invoice GET JSON may include fields not yet declared on Orval {@link Invoice} (OpenAPI lag).
 * Backend entity: `Invoice.InvoiceDataProvenance` (Persisted | DerivedFromPayment) on synthesized vs persisted rows.
 *
 * Do **not** infer provenance from heuristics (e.g. presence of sourcePaymentId). Use this reader only for explicit
 * response values; when absent, UI must state the contract gap (see {@link RKSv_ADMIN_CONTRACT_GAPS.invoiceDetailProvenance}).
 */
import type { Invoice } from '@/api/generated/model/invoice';

/** Runtime shape until OpenAPI adds `invoiceDataProvenance`. One intentional intersection read — not a second DTO. */
type InvoiceMaybeProvenance = Invoice & { invoiceDataProvenance?: unknown };

/**
 * Returns trimmed backend provenance string when present on the JSON body; otherwise `undefined`.
 * Typed `Invoice` from Orval does not include this key — cast is isolated here.
 */
export function readOptionalInvoiceDataProvenance(detail: Invoice): string | undefined {
  const v = (detail as InvoiceMaybeProvenance).invoiceDataProvenance;
  return typeof v === 'string' && v.trim() !== '' ? v.trim() : undefined;
}

/** Known backend discriminator values (C#); unknown strings pass through for honest display. */
export function formatInvoiceDataProvenanceForDisplay(raw: string): string {
  switch (raw) {
    case 'Persisted':
      return 'Persistiert (Rechnungszeile)';
    case 'DerivedFromPayment':
      return 'Aus Zahlung abgeleitet (keine persistierte Rechnungszeile)';
    default:
      return raw;
  }
}
