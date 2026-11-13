/**
 * Canonical invoice download file names.
 * PDF: invoice_{tenantSlug}_{registerNumber}_{yyyyMMdd_HHmmss}_{invoiceNumber}.pdf
 * CSV/Excel list: invoices_{tenantSlug}_{fromDate}_{toDate}_{yyyyMMdd_HHmmss}.{ext}
 */

export function buildInvoicePdfFileName(
  tenantSlug: string | null | undefined,
  registerNumber: string | null | undefined,
  invoiceNumber: string | null | undefined,
  at: Date = new Date()
): string {
  const stamp = formatLocalStamp(at);
  const slug = sanitizeInvoiceFileSegment(tenantSlug, 'tenant');
  const register = sanitizeInvoiceFileSegment(registerNumber, 'register');
  const number = sanitizeInvoiceFileSegment(invoiceNumber, 'invoice');
  return `invoice_${slug}_${register}_${stamp}_${number}.pdf`;
}

export function buildInvoiceListFileName(
  tenantSlug: string | null | undefined,
  fromDate: string | Date | null | undefined,
  toDate: string | Date | null | undefined,
  extension: 'csv' | 'xlsx' | 'xls' = 'csv',
  at: Date = new Date()
): string {
  const stamp = formatLocalStamp(at);
  const slug = sanitizeInvoiceFileSegment(tenantSlug, 'tenant');
  const from = formatDateOnly(fromDate);
  const to = formatDateOnly(toDate);
  return `invoices_${slug}_${from}_${to}_${stamp}.${extension}`;
}

export function sanitizeInvoiceFileSegment(
  value: string | null | undefined,
  fallback: string
): string {
  if (!value || !value.trim()) return fallback;
  const sanitized = value
    .trim()
    .replace(/[.\s/\\:]+/g, '_')
    .replace(/[^a-zA-Z0-9_-]/g, '')
    .replace(/^_+|_+$/g, '');
  return sanitized || fallback;
}

function formatLocalStamp(at: Date): string {
  const y = at.getFullYear();
  const m = String(at.getMonth() + 1).padStart(2, '0');
  const d = String(at.getDate()).padStart(2, '0');
  const hh = String(at.getHours()).padStart(2, '0');
  const mm = String(at.getMinutes()).padStart(2, '0');
  const ss = String(at.getSeconds()).padStart(2, '0');
  return `${y}${m}${d}_${hh}${mm}${ss}`;
}

function formatDateOnly(value: string | Date | null | undefined): string {
  if (!value) return 'all';
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) return 'all';
    // ISO or yyyy-MM-dd → yyyyMMdd
    const isoDay = /^(\d{4})-(\d{2})-(\d{2})/.exec(trimmed);
    if (isoDay) return `${isoDay[1]}${isoDay[2]}${isoDay[3]}`;
    const compact = /^(\d{8})$/.exec(trimmed);
    if (compact) return compact[1];
    const parsed = new Date(trimmed);
    if (!Number.isNaN(parsed.getTime())) return formatDateOnly(parsed);
    return 'all';
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}${m}${d}`;
}
