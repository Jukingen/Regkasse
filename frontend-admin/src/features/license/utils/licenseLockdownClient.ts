/**
 * Client-side helpers for mandant license lockdown (403 LICENSE_EXPIRED*).
 * Used by the axios interceptor (non-React) and {@link useLicenseGuard}.
 */
import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { DEFAULT_TEXT_LOCALE, type TextLocale, getCatalog } from '@/i18n/config';
import { getStoredLanguage } from '@/i18n/languageStorage';
import { notifyError, notifyWarning } from '@/lib/notificationService';

export const LICENSE_EXPIRED_ERROR_CODES = [
  'LICENSE_EXPIRED',
  'LICENSE_EXPIRED_WRITE_BLOCKED',
  'LICENSE_EXPIRED_USER_MGMT_BLOCKED',
  'LICENSE_LOCKED',
] as const;

export type LicenseExpiredApiPayload = {
  error?: string;
  Error?: string;
  code?: string;
  message?: string;
  Message?: string;
};

type LockdownToastCopy = {
  title: string;
  description: string;
  guardDescription: string;
  guardDescriptionWithAction: string;
  writeBlockedTitle: string;
  writeBlockedDescription: string;
  writeBlockedDescriptionGeneric: string;
};

const FALLBACK_DE: LockdownToastCopy = {
  title: 'Lizenz abgelaufen',
  description: 'Bitte verlängern Sie Ihre Lizenz, um diese Aktion durchzuführen.',
  guardDescription: 'Diese Aktion ist im gesperrten Zustand nicht verfügbar.',
  guardDescriptionWithAction: '„{{action}}“ ist im gesperrten Zustand nicht verfügbar.',
  writeBlockedTitle: 'Schreiboperation blockiert',
  writeBlockedDescription: '„{{operation}}“ ist im gesperrten Zustand nicht erlaubt.',
  writeBlockedDescriptionGeneric:
    'Schreiboperationen sind im gesperrten Zustand nicht erlaubt.',
};

function readLockdownToast(locale: TextLocale): LockdownToastCopy {
  const raw = getCatalog(locale).license as
    | {
        lockdownToast?: Partial<LockdownToastCopy>;
      }
    | undefined;
  const toast = raw?.lockdownToast;
  return {
    title: toast?.title?.trim() || FALLBACK_DE.title,
    description: toast?.description?.trim() || FALLBACK_DE.description,
    guardDescription: toast?.guardDescription?.trim() || FALLBACK_DE.guardDescription,
    guardDescriptionWithAction:
      toast?.guardDescriptionWithAction?.trim() || FALLBACK_DE.guardDescriptionWithAction,
    writeBlockedTitle: toast?.writeBlockedTitle?.trim() || FALLBACK_DE.writeBlockedTitle,
    writeBlockedDescription:
      toast?.writeBlockedDescription?.trim() || FALLBACK_DE.writeBlockedDescription,
    writeBlockedDescriptionGeneric:
      toast?.writeBlockedDescriptionGeneric?.trim() ||
      FALLBACK_DE.writeBlockedDescriptionGeneric,
  };
}

export function getLicenseLockdownToastCopy(locale?: TextLocale): LockdownToastCopy {
  const loc = locale ?? getStoredLanguage();
  const primary = readLockdownToast(loc);
  if (loc === DEFAULT_TEXT_LOCALE) return primary;
  const de = readLockdownToast(DEFAULT_TEXT_LOCALE);
  return {
    title: primary.title || de.title,
    description: primary.description || de.description,
    guardDescription: primary.guardDescription || de.guardDescription,
    guardDescriptionWithAction:
      primary.guardDescriptionWithAction || de.guardDescriptionWithAction,
    writeBlockedTitle: primary.writeBlockedTitle || de.writeBlockedTitle,
    writeBlockedDescription: primary.writeBlockedDescription || de.writeBlockedDescription,
    writeBlockedDescriptionGeneric:
      primary.writeBlockedDescriptionGeneric || de.writeBlockedDescriptionGeneric,
  };
}

function normalizeToken(value: unknown): string {
  return typeof value === 'string' ? value.trim().toUpperCase() : '';
}

/** True when the API response indicates mandant/deployment license write lockdown. */
export function isLicenseExpiredForbiddenPayload(
  data: LicenseExpiredApiPayload | null | undefined
): boolean {
  if (!data) return false;
  const tokens = [
    normalizeToken(data.error),
    normalizeToken(data.Error),
    normalizeToken(data.code),
  ].filter(Boolean);

  for (const token of tokens) {
    if ((LICENSE_EXPIRED_ERROR_CODES as readonly string[]).includes(token)) {
      return true;
    }
    if (token.includes('LICENSE_EXPIRED') || token === 'LICENSE_LOCKED') {
      return true;
    }
  }

  const message = `${data.message ?? ''} ${data.Message ?? ''}`.toLowerCase();
  return (
    message.includes('license has expired') ||
    message.includes('lizenz ist abgelaufen') ||
    message.includes('license_expired')
  );
}

export type HandleLicenseExpiredForbiddenOptions = {
  /** Open the global license renewal modal (default true). */
  openRenewalModal?: boolean;
  locale?: TextLocale;
};

/**
 * Shows the license-expired notification and optionally opens the renewal modal.
 * Safe to call from axios interceptors (uses notificationService bridge).
 */
export function handleLicenseExpiredForbidden(
  options?: HandleLicenseExpiredForbiddenOptions
): void {
  const copy = getLicenseLockdownToastCopy(options?.locale);
  notifyError(copy.title, {
    mode: 'notification',
    description: copy.description,
    duration: 10,
    key: 'license-expired-forbidden',
  });
  if (options?.openRenewalModal !== false) {
    openLicenseRenewalModal();
  }
}

/** Client-side guard toast when UI blocks a write before the request is sent. */
export function notifyLicenseGuardBlocked(actionLabel?: string, locale?: TextLocale): void {
  const copy = getLicenseLockdownToastCopy(locale);
  const description = actionLabel?.trim()
    ? copy.guardDescriptionWithAction.replace(/\{\{\s*action\s*\}\}/g, actionLabel.trim())
    : copy.guardDescription;
  notifyWarning(copy.title, {
    mode: 'notification',
    description,
    duration: 5,
    key: 'license-guard-blocked',
  });
}

/** Stronger error toast for explicit write-operation blocks (`guardWriteOperation`). */
export function notifyLicenseWriteBlocked(operationLabel?: string, locale?: TextLocale): void {
  const copy = getLicenseLockdownToastCopy(locale);
  const description = operationLabel?.trim()
    ? copy.writeBlockedDescription.replace(/\{\{\s*operation\s*\}\}/g, operationLabel.trim())
    : copy.writeBlockedDescriptionGeneric;
  notifyError(copy.writeBlockedTitle, {
    mode: 'notification',
    description,
    duration: 5,
    key: 'license-write-blocked',
  });
}
