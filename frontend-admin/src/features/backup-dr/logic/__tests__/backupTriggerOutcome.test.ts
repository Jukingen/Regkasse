import { describe, expect, it } from 'vitest';
import type { BackupTriggerResponseDto } from '@/api/generated/model';
import { describeBackupTriggerOutcome } from '@/features/backup-dr/logic/backupTriggerOutcome';

describe('describeBackupTriggerOutcome', () => {
  it('new queued run: success path from NewQueuedRunCreated', () => {
    const res: BackupTriggerResponseDto = {
      newQueuedRunCreated: true,
      duplicateExecutionPrevented: false,
    };
    expect(describeBackupTriggerOutcome(res)).toEqual({
      level: 'success',
      messageKey: 'backupDr.messages.backupNewQueued',
    });
  });

  it('duplicate active manual: info from DuplicateExecutionPrevented', () => {
    const res: BackupTriggerResponseDto = {
      newQueuedRunCreated: false,
      duplicateExecutionPrevented: true,
    };
    expect(describeBackupTriggerOutcome(res)).toEqual({
      level: 'info',
      messageKey: 'backupDr.messages.backupDuplicateActive',
    });
  });

  it('idempotent replay: both flags false', () => {
    const res: BackupTriggerResponseDto = {
      newQueuedRunCreated: false,
      duplicateExecutionPrevented: false,
    };
    expect(describeBackupTriggerOutcome(res)).toEqual({
      level: 'info',
      messageKey: 'backupDr.messages.backupIdempotentReplay',
    });
  });

  it('ambiguous when both true (does not assume extra DTO fields)', () => {
    const res: BackupTriggerResponseDto = {
      newQueuedRunCreated: true,
      duplicateExecutionPrevented: true,
    };
    expect(describeBackupTriggerOutcome(res)).toEqual({
      level: 'info',
      messageKey: 'backupDr.messages.backupTriggerAmbiguous',
    });
  });

  it('treats missing booleans as idempotent replay (no invented server state)', () => {
    expect(describeBackupTriggerOutcome({})).toEqual({
      level: 'info',
      messageKey: 'backupDr.messages.backupIdempotentReplay',
    });
  });
});
