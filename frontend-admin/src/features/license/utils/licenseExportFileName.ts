/**
 * Canonical license export names:
 * - single: license_{tenantSlug}_{yyyyMMdd_HHmmss}.txt
 * - multiple: licenses_{tenantSlug}_{yyyyMMdd_HHmmss}.{json|csv}
 */

export function buildSingleLicenseExportFileName(
  tenantSlug?: string | null,
  at: Date = new Date()
): string {
  const slug = sanitizeLicenseExportFileSegment(tenantSlug, 'deployment');
  const stamp = formatLocalStamp(at);
  return `license_${slug}_${stamp}.txt`;
}

export function buildLicensesExportFileName(
  tenantSlug?: string | null,
  format: 'json' | 'csv' | string = 'json',
  at: Date = new Date()
): string {
  const slug = sanitizeLicenseExportFileSegment(tenantSlug, 'deployment');
  const stamp = formatLocalStamp(at);
  const ext = format?.trim().toLowerCase() === 'csv' ? 'csv' : 'json';
  return `licenses_${slug}_${stamp}.${ext}`;
}

export function sanitizeLicenseExportFileSegment(
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
