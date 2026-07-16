/**
 * License renewal / extension targets (support funnel + optional FA deep link via env).
 */

import { buildAdminUrl } from './adminRoutes';
import { openLicenseExtension } from '../utils/openAdmin';
import { openMailtoUrl } from '../utils/openLink';

/** Shown in UI and used for mailto fallback. */
export const LICENSE_SUPPORT_EMAIL = 'support@regkasse.at';

/** Default mail subject for extension requests (German, operator-facing). */
export const LICENSE_RENEWAL_MAILTO_SUBJECT = 'Lizenzverlängerung';

/**
 * Full URL to open for Option B (e.g. `https://admin.example.com/admin/license?...`).
 * Takes precedence over `EXPO_PUBLIC_ADMIN_BASE_URL` when set.
 */
/** @deprecated Use `openLicenseExtension()` or `handleLicenseRenewal()` instead. */
export function getLicenseExtensionHttpUrl(machineHash?: string | null): string | undefined {
  console.warn('getLicenseExtensionHttpUrl is deprecated, use openLicenseExtension');
  return buildLicenseExtensionHttpUrl(machineHash);
}

export function buildLicenseExtensionHttpUrl(machineHash?: string | null): string | undefined {
  return buildAdminUrl('licenseExtend', { machineHash });
}

export type LicenseRenewalMailtoContext = {
  machineHash?: string | null;
  daysRemaining: number;
  isTrial: boolean;
  isExpired: boolean;
} | null;

async function getLicenseRenewalStatus(): Promise<LicenseRenewalMailtoContext> {
  try {
    const { apiClient } = await import('../services/api/config');
    const raw = await apiClient.get<Record<string, unknown>>('/health/license');
    return {
      machineHash: typeof raw.machineHash === 'string' ? raw.machineHash : null,
      daysRemaining:
        typeof raw.daysRemaining === 'number' && Number.isFinite(raw.daysRemaining)
          ? Math.max(0, Math.trunc(raw.daysRemaining))
          : 0,
      isTrial: raw.isTrial === true,
      isExpired: raw.isExpired === true,
    };
  } catch {
    return null;
  }
}

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

export async function handleLicenseRenewal(snapshot?: LicenseRenewalMailtoContext): Promise<boolean> {
  const currentSnapshot = snapshot ?? (await getLicenseRenewalStatus());
  const machineHash = currentSnapshot?.machineHash?.trim();

  if (machineHash) {
    return openLicenseExtension(machineHash);
  }

  return openMailtoUrl(buildLicenseRenewalMailtoUrl(currentSnapshot));
}
