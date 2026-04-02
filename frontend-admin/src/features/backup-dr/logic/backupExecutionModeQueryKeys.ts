/**
 * Execution-mode React Query anahtarı ve URL path — `axios` içermez (testlerde güvenle import edilir).
 * Uç tanımları: `backupExecutionModeApi.ts`.
 */

/** OpenAPI’ye eklendiğinde Orval ile aynı path dizesi kalır. */
export const BACKUP_EXECUTION_MODE_API_PATH = '/api/admin/backup/execution-mode' as const;

/**
 * Orval `getGetApiAdminBackupExecutionModeQueryKey` ile aynı biçim (tek path segmenti).
 */
export function getGetApiAdminBackupExecutionModeQueryKey() {
  return [BACKUP_EXECUTION_MODE_API_PATH] as const;
}
