import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  PitrRestoreApprovalError,
  buildPitrRestoreReason,
  triggerPitrRestoreWithApproval,
} from '@/features/backup/logic/pitrRestoreApproval';

const mockPostManualRestoreRequest = vi.fn();

vi.mock('@/features/backup-dr/logic/manualRestoreApi', () => ({
  postManualRestoreRequest: (body: unknown) => mockPostManualRestoreRequest(body),
}));

vi.mock('@/features/backup-dr/logic/manualRestorePresentation', () => ({
  defaultValidationDatabaseName: (d: Date) => `restore_validation_${d.toISOString().slice(0, 10)}`,
}));

describe('pitrRestoreApproval', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('builds reason with optional recovery metadata', () => {
    expect(
      buildPitrRestoreReason({
        targetTimeUtc: '2026-08-01T12:00:00.000Z',
        recoveryMethod: 'PITR',
        estimatedDataLossSeconds: 30,
      })
    ).toBe(
      'PITR targetTimeUtc=2026-08-01T12:00:00.000Z; recoveryMethod=PITR; estimatedDataLossSeconds=30'
    );

    expect(
      buildPitrRestoreReason({
        targetTimeUtc: '2026-08-01T12:00:00.000Z',
        recoveryMethod: null,
        estimatedDataLossSeconds: null,
      })
    ).toBe('PITR targetTimeUtc=2026-08-01T12:00:00.000Z');
  });

  it('rejects invalid validation', async () => {
    await expect(
      triggerPitrRestoreWithApproval({
        targetTime: new Date('2026-08-01T12:00:00Z'),
        validation: {
          isValid: false,
          message: 'out of range',
          baseBackupId: 'b1',
          baseBackupTimeUtc: null,
          targetTimeUtc: null,
          estimatedDataLossSeconds: null,
          recoveryMethod: null,
        },
      })
    ).rejects.toMatchObject({ code: 'INVALID_VALIDATION', name: 'PitrRestoreApprovalError' });
    expect(mockPostManualRestoreRequest).not.toHaveBeenCalled();
  });

  it('rejects missing base backup', async () => {
    await expect(
      triggerPitrRestoreWithApproval({
        targetTime: new Date('2026-08-01T12:00:00Z'),
        validation: {
          isValid: true,
          message: null,
          baseBackupId: '  ',
          baseBackupTimeUtc: null,
          targetTimeUtc: null,
          estimatedDataLossSeconds: null,
          recoveryMethod: 'PITR',
        },
      })
    ).rejects.toBeInstanceOf(PitrRestoreApprovalError);
  });

  it('queues validation-only restore on success', async () => {
    mockPostManualRestoreRequest.mockResolvedValue({ requestId: 'req-1' });
    const targetTime = new Date('2026-08-01T12:00:00.000Z');
    await triggerPitrRestoreWithApproval({
      targetTime,
      validation: {
        isValid: true,
        message: null,
        baseBackupId: 'backup-42',
        baseBackupTimeUtc: '2026-08-01T00:00:00Z',
        targetTimeUtc: targetTime.toISOString(),
        estimatedDataLossSeconds: 0,
        recoveryMethod: 'PITR',
      },
    });
    expect(mockPostManualRestoreRequest).toHaveBeenCalledWith({
      backupRunId: 'backup-42',
      targetDatabaseName: 'restore_validation_2026-08-01',
      validationOnly: true,
      reason: expect.stringContaining('PITR targetTimeUtc='),
    });
    expect(mockPostManualRestoreRequest.mock.calls[0]![0].reason).toContain(
      'recoveryMethod=PITR'
    );
  });
});
