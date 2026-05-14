import { Alert } from 'react-native';
import type { TFunction } from 'i18next';

import type { LicenseStatus } from '../hooks/useLicenseStatus';
import { isDevelopmentSimulationEnvironment } from '../src/config/devFlags';
import { deploymentLicenseAllows, LICENSE_DEPLOYMENT_FEATURE } from './licenseDeploymentFeatures';

let loggedDevLicenseBypass = false;

/**
 * Dev bundle only: skip POS license expiry / trial confirmation (matches backend Development bypass for local QA).
 */
export function areLicenseChecksBypassedInDevelopment(): boolean {
  if (!isDevelopmentSimulationEnvironment()) return false;
  if (!loggedDevLicenseBypass) {
    loggedDevLicenseBypass = true;
    console.warn('⚠️ DEVELOPMENT MODE: License checks bypassed');
  }
  return true;
}

export type LicenseCriticalActionKind = 'payment' | 'specialReceipt' | 'fiscalExport';

const TRIAL_CONFIRM_MAX_DAYS_REMAINING = 3;

/**
 * Returns true when the anonymous license read model indicates trial/demo-style hosting.
 */
export function isTrialLikeLicenseStatus(status: LicenseStatus | null | undefined): boolean {
  if (!status) return false;
  if (status.isTrial) return true;
  const mode = (status.mode ?? '').trim().toLowerCase();
  const licenseType = (status.licenseType ?? '').trim().toLowerCase();
  return mode === 'demo' || mode === 'trial' || licenseType === 'demo' || licenseType === 'trial';
}

/**
 * Returns true when payments / fiscal actions should be blocked before contacting support.
 */
export function isLicenseExpiredForCriticalActions(status: LicenseStatus | null | undefined): boolean {
  if (areLicenseChecksBypassedInDevelopment()) return false;
  if (!status) return false;
  if (status.isExpired) return true;
  const licenseType = (status.licenseType ?? '').trim().toLowerCase();
  return licenseType === 'expired';
}

function shouldConfirmTrialWindow(status: LicenseStatus): boolean {
  if (areLicenseChecksBypassedInDevelopment()) return false;
  if (!isTrialLikeLicenseStatus(status)) return false;
  const days = Number.isFinite(status.daysRemaining) ? Math.max(0, Math.floor(status.daysRemaining)) : 0;
  return days <= TRIAL_CONFIRM_MAX_DAYS_REMAINING;
}

/**
 * POS guard for fiscal payments, RKSV special receipts, or fiscal export flows.
 * When status is unknown (null), allows the action so transient network issues do not hard-block sales.
 * Pass `t` from `useTranslation('license')` so keys resolve under the `license` namespace.
 */
export function ensureLicenseAllowsCriticalAction(
  status: LicenseStatus | null,
  t: TFunction,
  _kind: LicenseCriticalActionKind
): Promise<boolean> {
  if (areLicenseChecksBypassedInDevelopment()) {
    return Promise.resolve(true);
  }

  if (!status) {
    return Promise.resolve(true);
  }

  if (isLicenseExpiredForCriticalActions(status)) {
    return new Promise((resolve) => {
      Alert.alert(
        t('criticalGuard.expiredTitle'),
        t('criticalGuard.expiredBody'),
        [{ text: t('criticalGuard.ok'), onPress: () => resolve(false) }]
      );
    });
  }

  const needsPosFiscal =
    _kind === 'payment' || _kind === 'specialReceipt' || _kind === 'fiscalExport';
  if (
    needsPosFiscal &&
    !deploymentLicenseAllows(status.enabledFeatures, LICENSE_DEPLOYMENT_FEATURE.PosFiscal)
  ) {
    return new Promise((resolve) => {
      Alert.alert(
        t('criticalGuard.featureDeniedTitle'),
        t('criticalGuard.featureDeniedBody', { featureId: LICENSE_DEPLOYMENT_FEATURE.PosFiscal }),
        [{ text: t('criticalGuard.ok'), onPress: () => resolve(false) }]
      );
    });
  }

  if (shouldConfirmTrialWindow(status)) {
    const days = Math.max(0, Math.floor(status.daysRemaining));
    return new Promise((resolve) => {
      Alert.alert(
        t('criticalGuard.trialSoonTitle'),
        t('criticalGuard.trialSoonBody', { count: days }),
        [
          { text: t('criticalGuard.cancel'), style: 'cancel', onPress: () => resolve(false) },
          { text: t('criticalGuard.proceed'), onPress: () => resolve(true) },
        ],
        { cancelable: true, onDismiss: () => resolve(false) }
      );
    });
  }

  return Promise.resolve(true);
}
