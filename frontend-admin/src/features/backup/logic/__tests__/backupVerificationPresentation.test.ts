import { describe, expect, it } from 'vitest';

import type { BackupRunResponseDto } from '@/api/generated/model';
import {
  isVerificationFailed,
  isVerificationPassed,
  resolveLatestVerification,
} from '@/features/backup/logic/backupVerificationPresentation';

describe('backupVerificationPresentation', () => {
  it('returns null when no verifications', () => {
    expect(resolveLatestVerification(null)).toBeNull();
    expect(resolveLatestVerification({} as BackupRunResponseDto)).toBeNull();
  });

  it('sorts by completedAt then startedAt descending', () => {
    const run = {
      verifications: [
        { status: 1, startedAt: '2026-08-01T00:00:00Z', completedAt: '2026-08-01T01:00:00Z' },
        { status: 2, startedAt: '2026-08-02T00:00:00Z', completedAt: '2026-08-02T01:00:00Z' },
      ],
    } as BackupRunResponseDto;
    expect(resolveLatestVerification(run)?.status).toBe(2);
  });

  it('detects passed/failed status codes', () => {
    expect(isVerificationPassed(1)).toBe(true);
    expect(isVerificationPassed(2)).toBe(false);
    expect(isVerificationFailed(2)).toBe(true);
    expect(isVerificationFailed(undefined)).toBe(false);
  });
});
