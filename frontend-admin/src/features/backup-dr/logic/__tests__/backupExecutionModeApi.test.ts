/**
 * `backupExecutionModeQueryKeys`: axios yok — path + React Query anahtarı Orval biçimiyle sabit.
 */
import { describe, expect, it } from 'vitest';
import {
  BACKUP_EXECUTION_MODE_API_PATH,
  getGetApiAdminBackupExecutionModeQueryKey,
} from '@/features/backup-dr/logic/backupExecutionModeQueryKeys';

describe('backupExecutionModeQueryKeys — Orval hizalama', () => {
  it('query key tek segment ve path sabiti ile aynı dize', () => {
    expect(getGetApiAdminBackupExecutionModeQueryKey()).toEqual([BACKUP_EXECUTION_MODE_API_PATH]);
    expect(BACKUP_EXECUTION_MODE_API_PATH).toBe('/api/admin/backup/execution-mode');
  });
});
