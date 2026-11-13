'use client';

import { useCallback } from 'react';
import { useRouter } from 'next/navigation';

import {
  notifyLicenseGuardBlocked,
  notifyLicenseWriteBlocked,
} from '@/features/license/utils/licenseLockdownClient';
import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { isLicenseLockdownActionAllowed } from '@/hooks/useLicenseMenuVisibility';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';

export type LicenseGuardOptions = {
  /** When true (default), also open the renewal modal on block. */
  openRenewalModal?: boolean;
  /** When true, navigate to `/license` after blocking (default false for `guard`). */
  navigateToLicense?: boolean;
};

/**
 * Blocks write UI actions when mandant license is Locked/Archived and shows a toast.
 *
 * Prefer `guard` / `guardWriteOperation` on click handlers — do **not** call them in
 * `disabled={...}` (that would toast on every render). Use `isLocked` / `canWrite` for UI state.
 *
 * @example
 * const { guard, guardWriteOperation, isLocked } = useLicenseGuard();
 * if (!guardWriteOperation(t('products.page.newProduct'))) return;
 * <Button disabled={isLocked} onClick={openCreate} />
 */
export function useLicenseGuard() {
  const { status } = useLicenseStatus();
  const { textLocale } = useI18n();
  const router = useRouter();

  const isLocked =
    status?.state === 'Locked' || status?.state === 'Archived';

  const openRenewalUi = useCallback(
    (options?: LicenseGuardOptions) => {
      if (options?.openRenewalModal !== false) {
        openLicenseRenewalModal();
      }
      if (options?.navigateToLicense) {
        router.push('/license');
      }
    },
    [router]
  );

  /** Soft block (warning toast) + optional renewal modal. Returns false when blocked. */
  const guard = useCallback(
    (action?: string, options?: LicenseGuardOptions) => {
      if (!isLocked) {
        return true;
      }
      notifyLicenseGuardBlocked(action, textLocale);
      openRenewalUi(options);
      return false;
    },
    [isLocked, openRenewalUi, textLocale]
  );

  /**
   * Explicit write gate (error toast). Same boolean contract as `guard`.
   * Opens renewal modal by default; set `navigateToLicense` to also route to `/license`.
   */
  const guardWriteOperation = useCallback(
    (operation?: string, options?: LicenseGuardOptions) => {
      if (!isLocked) {
        return true;
      }
      notifyLicenseWriteBlocked(operation, textLocale);
      openRenewalUi({
        openRenewalModal: options?.openRenewalModal,
        navigateToLicense: options?.navigateToLicense ?? true,
      });
      return false;
    },
    [isLocked, openRenewalUi, textLocale]
  );

  /** Run `callback` only when not locked; otherwise toast + navigate to license. */
  const guardAction = useCallback(
    (action: string, callback: () => void, options?: LicenseGuardOptions) => {
      if (
        !guard(action, {
          openRenewalModal: options?.openRenewalModal,
          navigateToLicense: options?.navigateToLicense ?? true,
        })
      ) {
        return;
      }
      callback();
    },
    [guard]
  );

  /**
   * Whether an operation key is allowed under lockdown.
   * Write ops are blocked; renew / export / read-style keys remain allowed
   * (see `isLicenseLockdownActionAllowed`).
   */
  const isOperationAllowed = useCallback(
    (operation: string) =>
      isLicenseLockdownActionAllowed(operation, { isLocked }),
    [isLocked]
  );

  return {
    guard,
    guardAction,
    guardWriteOperation,
    isOperationAllowed,
    isLocked,
    /** Convenience: writes are allowed only when license is not Locked/Archived. */
    canWrite: !isLocked,
    status,
  };
}
