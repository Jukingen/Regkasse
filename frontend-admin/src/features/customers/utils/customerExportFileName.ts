/**
 * Canonical customer export names:
 * customer_{tenantSlug}_{yyyyMMdd_HHmmss}.{csv|json}
 */

export function buildCustomerExportFileName(
  tenantSlug?: string | null,
  format: 'csv' | 'json' | string = 'csv',
  at: Date = new Date()
): string {
  const slug = sanitizeCustomerExportFileSegment(tenantSlug, 'tenant');
  const stamp = formatLocalStamp(at);
  const ext = format?.trim().toLowerCase() === 'json' ? 'json' : 'csv';
  return `customer_${slug}_${stamp}.${ext}`;
}

export function sanitizeCustomerExportFileSegment(
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
