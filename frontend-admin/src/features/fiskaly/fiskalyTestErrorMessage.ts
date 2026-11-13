import type { FiskalyReceiptError } from '@/features/fiskaly/api/fiskalyReceipts';

const ERROR_KEYS: Record<string, string> = {
  STARTBELEG_REQUIRED: 'tseFiskaly.test.errors.startbelegRequired',
  DUPLICATE_STARTBELEG: 'tseFiskaly.test.errors.duplicateStartbeleg',
  RKSV_DUPLICATE_STARTBELEG: 'tseFiskaly.test.errors.duplicateStartbeleg',
  RKSV_DUPLICATE_MONATSBELEG: 'tseFiskaly.test.errors.duplicateMonatsbeleg',
  RKSV_DUPLICATE_JAHRESBELEG: 'tseFiskaly.test.errors.duplicateJahresbeleg',
  DUPLICATE_OR_DECOMMISSIONED: 'tseFiskaly.test.errors.duplicateOrDecommissioned',
  INVALID_PERIOD: 'tseFiskaly.test.errors.invalidPeriod',
  PERIOD_NOT_COMPLETED: 'tseFiskaly.test.errors.periodNotCompleted',
  CASH_REGISTER_NOT_FOUND: 'tseFiskaly.test.errors.cashRegisterNotFound',
  CASH_REGISTER_ID_REQUIRED: 'tseFiskaly.test.errors.cashRegisterIdRequired',
  FISKALY_DISABLED: 'tseFiskaly.test.errors.fiskalyDisabled',
  FISKALY_NOT_CONFIGURED: 'tseFiskaly.test.errors.fiskalyNotConfigured',
  FISKALY_LIVE_BLOCKED: 'tseFiskaly.test.errors.fiskalyLiveBlocked',
  FISKALY_REGISTER_NOT_INITIALIZED: 'tseFiskaly.test.errors.registerNotInitialized',
  SPECIAL_RECEIPT_FAILED: 'tseFiskaly.test.errors.specialReceiptFailed',
};

export function fiskalyTestErrorTitleKey(code: string | undefined): string | undefined {
  if (!code) return undefined;
  return ERROR_KEYS[code];
}

export function displayFiskalyTestError(
  t: (key: string) => string,
  error: FiskalyReceiptError | null,
  fallbackKey: string
): { title: string; details?: string; code?: string } {
  if (!error) {
    return { title: t(fallbackKey) };
  }

  const localized = fiskalyTestErrorTitleKey(error.code);
  const title = localized ? t(localized) : error.message.trim() || t(fallbackKey);
  const details = error.details?.trim() && error.details.trim() !== title ? error.details.trim() : undefined;
  return { title, details, code: error.code || undefined };
}
