/**
 * Canonical DEP export download file name.
 * Format: dep-export_{tenantSlug}_{registerNumber}_{yyyyMMdd_HHmmss}.json
 */
export function buildDepExportFileName(
  tenantSlug: string | null | undefined,
  registerNumber: string | null | undefined,
  at: Date = new Date()
): string {
  const stamp = formatLocalStamp(at);
  const slug = sanitizeDepExportFileSegment(tenantSlug, 'tenant');
  const register = sanitizeDepExportFileSegment(registerNumber, 'register');
  return `dep-export_${slug}_${register}_${stamp}.json`;
}

export function sanitizeDepExportFileSegment(
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
