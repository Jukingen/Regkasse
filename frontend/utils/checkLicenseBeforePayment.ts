import { Alert } from 'react-native';
import type { TFunction } from 'i18next';

import { refreshPosStatusOverview } from '../services/pos/posStatusOverviewRefreshBridge';
import { getCachedLicenseStatus } from '../services/license/licenseStatusCache';
import {
  areLicenseChecksBypassedInDevelopment,
  ensureLicenseAllowsCriticalAction,
  isLicenseExpiredForCriticalActions,
} from './licenseCriticalActionGuard';

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
    Alert.alert('Lizenz abgelaufen', 'Bitte kontaktieren Sie Ihren Administrator');
    return false;
  }

  return ensureLicenseAllowsCriticalAction(status, t, 'payment');
}
