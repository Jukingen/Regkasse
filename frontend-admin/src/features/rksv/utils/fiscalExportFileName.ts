/**
 * Canonical fiscal export download file names.
 * Base: fiscal-export_{tenantSlug}_{registerNumber}_{yyyyMMdd_HHmmss}.{ext}
 * With profile: fiscal-export_{tenantSlug}_{registerNumber}_{profile}_{yyyyMMdd_HHmmss}.{ext}
 */

export function buildFiscalExportFileName(options: {
  tenantSlug?: string | null;
  registerNumber?: string | null;
  profileName?: string | null;
  extension?: 'json' | 'pdf';
  at?: Date;
}): string {
  const stamp = formatLocalStamp(options.at ?? new Date());
  const slug = sanitizeFiscalExportFileSegment(options.tenantSlug, 'tenant');
  const register = sanitizeFiscalExportFileSegment(options.registerNumber, 'register');
  const ext = options.extension ?? 'json';
  const profile = options.profileName?.trim()
    ? sanitizeFiscalExportFileSegment(options.profileName, 'profile')
    : null;

  if (profile) {
    return `fiscal-export_${slug}_${register}_${profile}_${stamp}.${ext}`;
  }
  return `fiscal-export_${slug}_${register}_${stamp}.${ext}`;
}

export function sanitizeFiscalExportFileSegment(
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
