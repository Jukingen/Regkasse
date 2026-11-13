import type { TFunction } from 'i18next';
import { Alert } from 'react-native';

import i18n from '../i18n';
import {
  areLicenseChecksBypassedInDevelopment,
  ensureLicenseAllowsCriticalAction,
  isLicenseExpiredForCriticalActions,
} from './licenseCriticalActionGuard';
import { getCachedLicenseStatus } from '../services/license/licenseStatusCache';
import { refreshPosStatusOverview } from '../services/pos/posStatusOverviewRefreshBridge';

/**
 * Fresh license gate for payment: refreshes overview once, then runs critical-action guards.
 */
export async function checkLicenseBeforePayment(t: TFunction): Promise<boolean> {
  if (areLicenseChecksBypassedInDevelopment()) {
    return true;
  }

  await refreshPosStatusOverview(true);
  const status = getCachedLicenseStatus();

  if (status && isLicenseExpiredForCriticalActions(status)) {
    Alert.alert(
      i18n.t('payment:errors.licenseLockedTitle'),
      i18n.t('payment:errors.licenseLocked')
    );
    return false;
  }

  return await ensureLicenseAllowsCriticalAction(status, t, 'payment');
}
