import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Platform } from 'react-native';
import { useRouter } from 'expo-router';

import { TENANT_ARCHIVE_AFTER_DAYS } from '../constants/licenseGracePeriod';
import { useAuth } from '../contexts/AuthContext';
import { useLicenseStatus } from '../contexts/LicenseStatusContext';
import { useMandantLicenseWarning } from '../contexts/MandantLicenseWarningContext';
import {
  cancelLicenseReminders,
  scheduleLicenseReminders,
  type LicenseReminderCopy,
} from '../services/pushNotification';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import { saveLicenseLockoutSnapshot } from '../utils/licenseLockoutSnapshot';
import type { LicenseReminderDay } from '../utils/licenseReminderSchedule';

function resolveExpiryDate(
  validUntil: string | null | undefined,
  expiryDate: string | null | undefined
): Date | null {
  const raw = [validUntil, expiryDate]
    .map((value) => value?.trim())
    .find((value) => value != null && value.length > 0);
  if (!raw) return null;
  const parsed = new Date(raw);
  return Number.isFinite(parsed.getTime()) ? parsed : null;
}

function isMandantLockedOrArchived(args: {
  isLocked: boolean;
  canAccess: boolean;
  daysOverdue: number;
}): boolean {
  if (args.isLocked || !args.canAccess) return true;
  return args.daysOverdue > TENANT_ARCHIVE_AFTER_DAYS;
}

/**
 * Schedules local license-expiry reminders and shows a one-shot Locked/Archived alert.
 * Must run under Auth + license status providers.
 */
export function useLicenseExpiryNotifications(): void {
  const { t } = useTranslation('license');
  const router = useRouter();
  const { isAuthenticated, user, logout } = useAuth();
  const { status } = useLicenseStatus();
  const { state: mandantWarning } = useMandantLicenseWarning();
  const lockdownAlertShownRef = useRef(false);
  const lastScheduledExpiryRef = useRef<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated || user?.mustChangePasswordOnNextLogin) {
      lockdownAlertShownRef.current = false;
      lastScheduledExpiryRef.current = null;
      cancelLicenseReminders().catch(() => undefined);
      return;
    }

    if (areLicenseChecksBypassedInDevelopment()) {
      return;
    }

    const expiry = resolveExpiryDate(mandantWarning?.validUntil, status?.expiryDate);
    const expiryKey = expiry?.toISOString() ?? null;

    if (
      expiry &&
      expiry.getTime() > Date.now() &&
      Platform.OS !== 'web' &&
      lastScheduledExpiryRef.current !== expiryKey
    ) {
      lastScheduledExpiryRef.current = expiryKey;
      const copy: LicenseReminderCopy = {
        titleForDays: (days: LicenseReminderDay) => {
          switch (days) {
            case 30:
              return t('pushReminders.title30');
            case 15:
              return t('pushReminders.title15');
            case 7:
              return t('pushReminders.title7');
            case 1:
              return t('pushReminders.title1');
            default:
              return t('pushReminders.title7');
          }
        },
        body: t('pushReminders.body'),
      };
      scheduleLicenseReminders(expiry, copy).catch(() => {
        lastScheduledExpiryRef.current = null;
      });
    }

    if (!mandantWarning) return;
    if (
      !isMandantLockedOrArchived({
        isLocked: mandantWarning.isLocked,
        canAccess: mandantWarning.canAccess,
        daysOverdue: mandantWarning.daysOverdue,
      })
    ) {
      return;
    }

    if (lockdownAlertShownRef.current) return;
    lockdownAlertShownRef.current = true;

    void (async () => {
      await saveLicenseLockoutSnapshot(mandantWarning.daysOverdue);
      try {
        await logout();
      } catch {
        // still navigate to lockout screen
      }
      router.replace('/(auth)/license-expired');
    })();
  }, [
    isAuthenticated,
    user?.mustChangePasswordOnNextLogin,
    mandantWarning,
    status?.expiryDate,
    t,
    logout,
    router,
  ]);
}
