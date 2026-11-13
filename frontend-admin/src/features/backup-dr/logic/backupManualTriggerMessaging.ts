import axios from 'axios';

import { translateLimitExceededError } from '@/shared/errors/limitExceededMessage';

type BackupTriggerTranslateFn = (key: string, options?: Record<string, string | number>) => string;

export function triggerErrorMessageBackupDashboard(
  err: unknown,
  t: BackupTriggerTranslateFn
): string {
  if (axios.isAxiosError(err)) {
    const s = err.response?.status;
    if (s === 403) return t('backupDr.errors.forbiddenTrigger');
    if (s === 401) return t('backupDr.errors.unauthorizedTrigger');
    if (s === 409) {
      const limitMsg = translateLimitExceededError(t, err);
      if (limitMsg) return limitMsg;
      return t('backupDr.errors.conflictTrigger');
    }
    if (s === 422) return t('backupDr.errors.validationTrigger');
    if (s !== undefined && s >= 500) return t('backupDr.errors.serverTrigger');
  }
  return t('backupDr.errors.triggerFailed');
}
