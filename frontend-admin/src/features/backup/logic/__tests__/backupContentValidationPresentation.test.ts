import { describe, expect, it } from 'vitest';

import type { BackupRunResponseDto } from '@/api/generated/model';
import {
  contentStatusFromVerification,
  isContentValidationPositive,
  resolveContentValidationBadgeStatus,
  resolveLatestContentVerification,
} from '@/features/backup/logic/backupContentValidationPresentation';

describe('backupContentValidationPresentation', () => {
  const run = {
    verifications: [
      {
        verifierSource: 'content_validation',
        status: 1,
        startedAt: '2026-08-01T10:00:00Z',
        completedAt: '2026-08-01T10:01:00Z',
      },
      {
        verifierSource: 'content_validation',
        status: 2,
        startedAt: '2026-08-02T10:00:00Z',
        completedAt: '2026-08-02T10:01:00Z',
      },
      {
        verifierSource: 'checksum',
        status: 1,
        startedAt: '2026-08-03T10:00:00Z',
        completedAt: '2026-08-03T10:01:00Z',
      },
    ],
  } as BackupRunResponseDto;

  it('picks latest content_validation verification', () => {
    const latest = resolveLatestContentVerification(run);
    expect(latest?.status).toBe(2);
    expect(resolveLatestContentVerification(null)).toBeNull();
    expect(resolveLatestContentVerification({ verifications: [] } as BackupRunResponseDto)).toBeNull();
  });

  it('maps verification status to Passed/Failed', () => {
    expect(contentStatusFromVerification({ status: 1 } as never)).toBe('Passed');
    expect(contentStatusFromVerification({ status: 2 } as never)).toBe('Failed');
    expect(contentStatusFromVerification({ status: 0 } as never)).toBeNull();
    expect(contentStatusFromVerification(null)).toBeNull();
  });

  it('prefers session status for badge', () => {
    expect(resolveContentValidationBadgeStatus(run, 'Partial')).toBe('Partial');
    expect(resolveContentValidationBadgeStatus(run)).toBe('Failed');
  });

  it('treats only passed as positive', () => {
    expect(isContentValidationPositive('Passed')).toBe(true);
    expect(isContentValidationPositive('Failed')).toBe(false);
    expect(isContentValidationPositive(null)).toBe(false);
  });
});
