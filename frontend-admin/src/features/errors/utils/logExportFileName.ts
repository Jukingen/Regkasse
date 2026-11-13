/**
 * Canonical error/application log export names:
 * log_{tenantSlug}_{yyyyMMdd_HHmmss}.{txt|csv|json}
 */

export function buildLogExportFileName(
  tenantSlug?: string | null,
  format: 'txt' | 'csv' | 'json' | string = 'txt',
  at: Date = new Date()
): string {
  const slug = sanitizeLogExportFileSegment(tenantSlug, 'deployment');
  const stamp = formatLocalStamp(at);
  const normalized = format?.trim().toLowerCase();
  const ext = normalized === 'csv' ? 'csv' : normalized === 'json' ? 'json' : 'txt';
  return `log_${slug}_${stamp}.${ext}`;
}

export function sanitizeLogExportFileSegment(
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
