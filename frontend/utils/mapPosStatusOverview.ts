import { normalizeLicenseDaysRemaining } from './licenseExpiryRemaining';
import { normalizeRksvEnvironmentStatus } from './normalizeRksvEnvironment';
import { parsePosCashRegisterContextDto } from './posCashRegisterReadinessParse';
import type { LicensePublicStatusDto } from '../api/license';
import { TENANT_WARNING_DAYS_BEFORE_EXPIRY } from '../constants/licenseGracePeriod';
import type {
  PosStatusLicenseHealthDto,
  PosStatusOverviewDto,
} from '../services/api/posStatusOverviewTypes';
import type { LicenseStatus } from '../services/license/licenseStatusCache';
import type { MandantLicenseWarningState } from '../types/mandantLicenseWarning';

function inferTrialFromPublic(p: LicensePublicStatusDto): boolean {
  const lt = (p.licenseType ?? '').trim().toLowerCase();
  const m = (p.mode ?? '').trim().toLowerCase();
  return lt === 'trial' || lt === 'demo' || m === 'trial' || m === 'demo';
}

function inferPaidFromPublic(p: LicensePublicStatusDto): boolean {
  const lt = (p.licenseType ?? '').trim().toLowerCase();
  const m = (p.mode ?? '').trim().toLowerCase();
  if (lt === 'licensed' || lt === 'paid') return true;
  if (m === 'production' && p.isValid === true && p.isExpired !== true) return true;
  return false;
}

export function mapOverviewLicenseToStatus(
  license: LicensePublicStatusDto,
  health: PosStatusLicenseHealthDto
): LicenseStatus {
  const publicPaid = inferPaidFromPublic(license);
  const mergedTrial = publicPaid ? false : inferTrialFromPublic(license);
  const mergedValid = license.isValid === true || health.isValid === true;
  const publicExpired = license.isExpired === true;
  const operationalTrialFromPublic =
    inferTrialFromPublic(license) && license.isValid === true && !publicExpired;
  const mergedExpired = operationalTrialFromPublic
    ? false
    : publicPaid
      ? publicExpired
      : publicExpired || health.isExpired === true;

  const days =
    typeof license.daysRemaining === 'number' && Number.isFinite(license.daysRemaining)
      ? Math.max(0, normalizeLicenseDaysRemaining(license.daysRemaining))
      : Math.max(0, normalizeLicenseDaysRemaining(health.daysRemaining));

  return {
    isValid: mergedValid,
    isTrial: mergedTrial,
    isExpired: mergedExpired,
    daysRemaining: days,
    expiryDate:
      license.validUntil ??
      (health.expiryDate && health.expiryDate.length > 0 ? health.expiryDate : null),
    machineHash: health.machineHash ?? '',
    licenseType: license.licenseType ?? null,
    mode: license.mode ?? null,
    enabledFeatures: license.features?.length ? [...license.features] : null,
  };
}

export function mapOverviewToMandantWarning(
  license: LicensePublicStatusDto
): MandantLicenseWarningState | null {
  if (license.canAccess == null && license.isInGracePeriod !== true) {
    return null;
  }

  const daysRemaining =
    typeof license.daysRemaining === 'number' && Number.isFinite(license.daysRemaining)
      ? Math.max(0, normalizeLicenseDaysRemaining(license.daysRemaining))
      : 0;
  const gracePeriodRemaining =
    typeof license.gracePeriodRemaining === 'number' &&
    Number.isFinite(license.gracePeriodRemaining)
      ? Math.max(0, normalizeLicenseDaysRemaining(license.gracePeriodRemaining))
      : 0;

  return {
    daysRemaining,
    daysOverdue:
      typeof license.daysOverdue === 'number' && Number.isFinite(license.daysOverdue)
        ? Math.max(0, Math.trunc(license.daysOverdue))
        : 0,
    gracePeriodRemaining,
    isInGracePeriod: license.isInGracePeriod === true,
    isLocked: license.isLocked === true,
    canAccess: license.canAccess !== false,
    statusMessage: license.statusMessage ?? null,
    lockDate:
      typeof license.lockDate === 'string' && license.lockDate.length > 0 ? license.lockDate : null,
    restrictions: Array.isArray(license.restrictions) ? [...license.restrictions] : [],
    validUntil:
      typeof license.validUntil === 'string' && license.validUntil.length > 0
        ? license.validUntil
        : null,
  };
}

export function deriveMandantWarningFlags(state: MandantLicenseWarningState | null): {
  shouldShowGrace: boolean;
  shouldShowPreExpiry: boolean;
} {
  const shouldShowGrace = state?.isInGracePeriod === true && (state.gracePeriodRemaining ?? 0) >= 0;
  const shouldShowPreExpiry =
    state != null &&
    !state.isInGracePeriod &&
    state.daysRemaining > 0 &&
    state.daysRemaining <= TENANT_WARNING_DAYS_BEFORE_EXPIRY;
  return { shouldShowGrace, shouldShowPreExpiry };
}

export function normalizePosStatusOverview(raw: unknown): PosStatusOverviewDto {
  const body = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const licenseRaw = body.license ?? body.License;
  const healthRaw = body.healthLicense ?? body.HealthLicense;
  const settingsRaw = body.settings ?? body.Settings;
  const cashRegisterRaw = body.cashRegister ?? body.CashRegister;
  const rksvRaw = body.rksvEnvironment ?? body.RksvEnvironment;

  const license =
    licenseRaw && typeof licenseRaw === 'object'
      ? (licenseRaw as LicensePublicStatusDto)
      : ({} as LicensePublicStatusDto);

  const health =
    healthRaw && typeof healthRaw === 'object'
      ? (healthRaw as PosStatusLicenseHealthDto)
      : {
          isValid: false,
          isTrial: false,
          isExpired: true,
          daysRemaining: 0,
          expiryDate: null,
          machineHash: '',
        };

  const settings =
    settingsRaw && typeof settingsRaw === 'object'
      ? {
          cashRegisterId:
            typeof (settingsRaw as Record<string, unknown>).cashRegisterId === 'string'
              ? ((settingsRaw as Record<string, unknown>).cashRegisterId as string)
              : typeof (settingsRaw as Record<string, unknown>).CashRegisterId === 'string'
                ? ((settingsRaw as Record<string, unknown>).CashRegisterId as string)
                : null,
          settingsVersion: Number(
            (settingsRaw as Record<string, unknown>).settingsVersion ??
              (settingsRaw as Record<string, unknown>).SettingsVersion ??
              0
          ),
          updatedAtUtc: String(
            (settingsRaw as Record<string, unknown>).updatedAtUtc ??
              (settingsRaw as Record<string, unknown>).UpdatedAtUtc ??
              ''
          ),
        }
      : { cashRegisterId: null, settingsVersion: 0, updatedAtUtc: '' };

  return {
    serverTimeUtc: String(body.serverTimeUtc ?? body.ServerTimeUtc ?? ''),
    license,
    healthLicense: health,
    cashRegister: parsePosCashRegisterContextDto(cashRegisterRaw),
    settings,
    rksvEnvironment: normalizeRksvEnvironmentStatus(rksvRaw),
  };
}
