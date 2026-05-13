/**
 * License renewal / extension targets (support funnel + optional FA deep link via env).
 */

/** Shown in UI and used for mailto fallback. */
export const LICENSE_SUPPORT_EMAIL = 'support@regkasse.at';

/** Default mail subject for extension requests (German, operator-facing). */
export const LICENSE_RENEWAL_MAILTO_SUBJECT = 'Lizenzverlängerung';

function trimEnv(value: string | undefined): string | undefined {
  const t = value?.trim();
  return t && t.length > 0 ? t : undefined;
}

/**
 * Full URL to open for Option B (e.g. `https://admin.example.com/admin/license?...`).
 * Takes precedence over `EXPO_PUBLIC_ADMIN_BASE_URL` when set.
 */
export function getLicenseExtensionHttpUrl(): string | undefined {
  const explicit = trimEnv(process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL);
  if (explicit) {
    return explicit;
  }
  const base = trimEnv(process.env.EXPO_PUBLIC_ADMIN_BASE_URL);
  if (!base) {
    return undefined;
  }
  const normalizedBase = base.replace(/\/+$/, '');
  return `${normalizedBase}/admin/license`;
}

export type LicenseRenewalMailtoContext = {
  machineHash: string;
  daysRemaining: number;
  isTrial: boolean;
  isExpired: boolean;
} | null;

/**
 * mailto: URL with pre-filled German body for support (fingerprint + remaining days when known).
 */
export function buildLicenseRenewalMailtoUrl(snapshot: LicenseRenewalMailtoContext): string {
  const subject = encodeURIComponent(LICENSE_RENEWAL_MAILTO_SUBJECT);
  const lines: string[] = ['Bitte um Verlängerung der Regkasse-Lizenz.'];
  if (snapshot?.machineHash) {
    lines.push('');
    lines.push(`Geräte-Fingerabdruck (SHA-256): ${snapshot.machineHash}`);
  }
  if (snapshot != null) {
    lines.push('');
    lines.push(`Restlaufzeit (Tage): ${snapshot.daysRemaining}`);
    lines.push(`Modus: ${snapshot.isTrial ? 'Testversion' : snapshot.isExpired ? 'abgelaufen' : 'Vollversion'}`);
  }
  const body = encodeURIComponent(lines.join('\n'));
  return `mailto:${LICENSE_SUPPORT_EMAIL}?subject=${subject}&body=${body}`;
}
