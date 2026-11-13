/**
 * Invoice line items: OpenAPI types this field as `unknown | null` (see invoiceInvoiceItems.ts).
 * This module only classifies runtime shapes for display — it does not assert a line-item DTO.
 */
import type { Invoice } from '@/api/generated/model';

export type InvoiceItemsDisplayResult =
  | { kind: 'rows'; rows: readonly unknown[] }
  | { kind: 'parse_error'; message: string }
  | { kind: 'unsupported_primitive'; primitive: string };

/**
 * Normalize `Invoice.invoiceItems` for JSON preview / table rendering without inventing typed line items.
 */
export function normalizeInvoiceItemsForDisplay(
  raw: Invoice['invoiceItems']
): InvoiceItemsDisplayResult {
  if (raw == null) {
    return { kind: 'rows', rows: [] };
  }
  if (typeof raw === 'string') {
    const t = raw.trim();
    if (t === '') {
      return { kind: 'rows', rows: [] };
    }
    try {
      const parsed: unknown = JSON.parse(t);
      return shapeAfterJsonParse(parsed);
    } catch (e) {
      const message = e instanceof Error ? e.message : 'JSON.parse failed';
      return { kind: 'parse_error', message };
    }
  }
  if (Array.isArray(raw)) {
    return { kind: 'rows', rows: raw };
  }
  if (typeof raw === 'object') {
    return { kind: 'rows', rows: [raw] };
  }
  return { kind: 'unsupported_primitive', primitive: typeof raw };
}

function shapeAfterJsonParse(parsed: unknown): InvoiceItemsDisplayResult {
  if (parsed == null) {
    return { kind: 'rows', rows: [] };
  }
  if (Array.isArray(parsed)) {
    return { kind: 'rows', rows: parsed };
  }
  if (typeof parsed === 'object') {
    return { kind: 'rows', rows: [parsed] };
  }
  return { kind: 'unsupported_primitive', primitive: typeof parsed };
}
