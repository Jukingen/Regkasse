import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import {
  computeBackupRunDurationMinutes,
  filterBackupRunsByTenantIdempotency,
  isBackupRunFailed,
  resolveBackupRunStatusUiKey,
  sumArtifactBytes,
} from '@/features/backup/logic/backupRunTablePresentation';

describe('backupRunTablePresentation', () => {
  it('maps API status numbers to UI keys', () => {
    expect(resolveBackupRunStatusUiKey(BackupRunStatus.NUMBER_3)).toBe('succeeded');
    expect(resolveBackupRunStatusUiKey(BackupRunStatus.NUMBER_5)).toBe('verificationFailed');
  });

  it('computes duration in minutes', () => {
    const minutes = computeBackupRunDurationMinutes(
      '2026-01-01T10:00:00.000Z',
      '2026-01-01T10:06:00.000Z'
    );
    expect(minutes).toBeCloseTo(6, 5);
  });

  it('sums artifact bytes', () => {
    expect(sumArtifactBytes([{ byteSize: 100 }, { byteSize: 50 }])).toBe(150);
  });

  it('filters manual runs by tenant idempotency key', () => {
    const rows = [
      { idempotencyKey: 'manual-tenant-aaa-1' },
      { idempotencyKey: 'manual-tenant-bbb-2' },
      { idempotencyKey: 'manual-all-tenants-3' },
    ];
    expect(filterBackupRunsByTenantIdempotency(rows as any, 'bbb')).toHaveLength(1);
  });

  it('detects failed terminal statuses', () => {
    expect(isBackupRunFailed(BackupRunStatus.NUMBER_4)).toBe(true);
    expect(isBackupRunFailed(BackupRunStatus.NUMBER_3)).toBe(false);
  });
});
