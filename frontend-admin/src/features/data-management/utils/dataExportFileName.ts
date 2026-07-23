/**
 * Canonical GDPR data-export ZIP names:
 * data-export_{tenantSlug}_{yyyyMMdd_HHmmss}.zip
 */

export function buildDataExportFileName(
  tenantSlug?: string | null,
  at: Date = new Date()
): string {
  const slug = sanitizeDataExportFileSegment(tenantSlug, 'tenant');
  const stamp = formatLocalStamp(at);
  return `data-export_${slug}_${stamp}.zip`;
}

export function sanitizeDataExportFileSegment(
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
