import { describe, expect, it } from 'vitest';

import { normalizeBackupComplianceStatusResponse } from '@/features/backup/logic/backupComplianceStatusApi';

describe('normalizeBackupComplianceStatusResponse', () => {
  it('accepts a valid API payload', () => {
    const result = normalizeBackupComplianceStatusResponse({
      total: 1,
      compliant: 1,
      nonCompliant: 0,
      allCompliant: true,
      lastCheckUtc: '2026-08-08T12:00:00Z',
      backups: [
        {
          backupRunId: '11111111-1111-1111-1111-111111111111',
          date: '2026-08-01T10:00:00Z',
          status: 'Succeeded',
          compliant: true,
          reason: 'system_dump_hash_present',
        },
      ],
    });

    expect(result.total).toBe(1);
    expect(result.backups).toHaveLength(1);
    expect(result.backups[0]?.backupRunId).toBe('11111111-1111-1111-1111-111111111111');
  });

  it('rejects non-object payloads', () => {
    expect(() => normalizeBackupComplianceStatusResponse(null)).toThrow(/expected an object/);
  });

  it('rejects non-array backups', () => {
    expect(() =>
      normalizeBackupComplianceStatusResponse({
        total: 0,
        backups: { bad: true },
      })
    ).toThrow(/backups must be an array/);
  });
});
