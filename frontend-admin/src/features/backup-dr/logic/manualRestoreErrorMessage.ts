import axios from 'axios';

export function manualRestoreErrorMessage(
  err: unknown,
  t: (k: string) => string,
): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { error?: string; code?: string } | undefined;
    if (typeof data?.error === 'string' && data.error.trim()) return data.error;
    const s = err.response?.status;
    if (s === 403) return t('backupDr.errors.forbiddenTrigger');
    if (s === 401) return t('backupDr.errors.unauthorizedTrigger');
    if (s === 400) return t('backupDr.manualRestore.errors.badRequest');
    if (s === 409) return t('backupDr.errors.conflictTrigger');
    if (s === 503) return t('backupDr.manualRestore.errors.disabled');
    if (s !== undefined && s >= 500) return t('backupDr.errors.serverTrigger');
  }
  return t('backupDr.manualRestore.errors.generic');
}
