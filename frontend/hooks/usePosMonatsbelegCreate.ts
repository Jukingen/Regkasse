import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLicenseStatus } from './useLicenseStatus';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import {
  alertPosMonatsbelegCreateError,
  alertPosMonatsbelegCreateSuccess,
  createPosMonatsbelegAndPrint,
  requestPosMonatsbelegCreate,
} from '../utils/createPosMonatsbeleg';
import { ensureLicenseAllowsCriticalAction } from '../utils/licenseCriticalActionGuard';

export type PosMonatsbelegCreateArgs = {
  cashRegisterId: string;
  year: number;
  month: number;
  /** Called after successful create + readiness refresh (e.g. reload banner status). */
  onAfterSuccess?: () => void | Promise<void>;
};

/**
 * Shared POS Monatsbeleg / December Jahresbeleg create flow (license gate, API, print, alerts).
 * Used by the ensure-ready block modal and cash-register reminder banners.
 */
export function usePosMonatsbelegCreate() {
  const [busy, setBusy] = useState(false);
  const { t } = useTranslation('license');
  const { status: licenseSnapshot } = useLicenseStatus();
  const { refreshAsync } = usePosRegisterReadiness();

  const runCreate = useCallback(
    async (args: PosMonatsbelegCreateArgs): Promise<boolean> => {
      const registerId = args.cashRegisterId.trim();
      if (!registerId) return false;

      const licenseOk = await ensureLicenseAllowsCriticalAction(
        licenseSnapshot,
        t,
        'specialReceipt'
      );
      if (!licenseOk) return false;

      setBusy(true);
      try {
        const result = await createPosMonatsbelegAndPrint({
          cashRegisterId: registerId,
          year: args.year,
          month: args.month,
        });
        await refreshAsync();
        await args.onAfterSuccess?.();
        alertPosMonatsbelegCreateSuccess(result);
        return true;
      } catch (e: unknown) {
        alertPosMonatsbelegCreateError(e, args.month === 12);
        return false;
      } finally {
        setBusy(false);
      }
    },
    [licenseSnapshot, refreshAsync, t]
  );

  const requestCreate = useCallback(
    (args: PosMonatsbelegCreateArgs) => {
      if (!args.cashRegisterId.trim()) return;
      requestPosMonatsbelegCreate({
        year: args.year,
        month: args.month,
        run: () => {
          void runCreate(args);
        },
      });
    },
    [runCreate]
  );

  return { busy, runCreate, requestCreate };
}
