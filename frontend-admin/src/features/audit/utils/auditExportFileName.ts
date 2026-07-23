/**
 * Canonical audit log export names:
 * audit_{tenantSlug}_{from}_{to}_{yyyyMMdd_HHmmss}.{json|csv}
 */

export function buildAuditExportFileName(options: {
  tenantSlug?: string | null;
  fromDate?: string | Date | null;
  toDate?: string | Date | null;
  format?: 'json' | 'csv' | 'excel' | string;
  at?: Date;
}): string {
  const slug = sanitizeAuditExportFileSegment(options.tenantSlug, 'tenant');
  const from = formatAuditDateSegment(options.fromDate);
  const to = formatAuditDateSegment(options.toDate);
  const stamp = formatLocalStamp(options.at ?? new Date());
  const ext = normalizeAuditExportExtension(options.format);
  return `audit_${slug}_${from}_${to}_${stamp}.${ext}`;
}

export function normalizeAuditExportExtension(format?: string | null): 'json' | 'csv' {
  return format?.trim().toLowerCase() === 'json' ? 'json' : 'csv';
}

export function sanitizeAuditExportFileSegment(
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

function formatAuditDateSegment(value?: string | Date | null): string {
  if (!value) return 'all';
  if (value instanceof Date) {
    if (Number.isNaN(value.getTime())) return 'all';
    return formatDay(value);
  }
  const trimmed = value.trim();
  if (!trimmed) return 'all';
  // Already yyyyMMdd or yyyy-MM-dd
  const compact = trimmed.replace(/-/g, '');
  if (/^\d{8}$/.test(compact)) return compact;
  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return 'all';
  return formatDay(parsed);
}

function formatDay(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}${m}${d}`;
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
