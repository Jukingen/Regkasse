import axios from "axios";

export function triggerErrorMessageBackupDashboard(
  err: unknown,
  t: (k: string) => string,
): string {
  if (axios.isAxiosError(err)) {
    const s = err.response?.status;
    if (s === 403) return t("backupDr.errors.forbiddenTrigger");
    if (s === 401) return t("backupDr.errors.unauthorizedTrigger");
    if (s === 409) return t("backupDr.errors.conflictTrigger");
    if (s === 422) return t("backupDr.errors.validationTrigger");
    if (s !== undefined && s >= 500) return t("backupDr.errors.serverTrigger");
  }
  return t("backupDr.errors.triggerFailed");
}
