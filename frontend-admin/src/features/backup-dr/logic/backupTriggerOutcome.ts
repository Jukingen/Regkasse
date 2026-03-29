/**
 * Manuel yedek tetikleme yanıtı: yalnızca Orval `BackupTriggerResponseDto` alanlarına dayanır.
 */

import type { BackupTriggerResponseDto } from '@/api/generated/model';

export type BackupTriggerFeedbackLevel = 'success' | 'info';

export interface BackupTriggerFeedback {
  level: BackupTriggerFeedbackLevel;
  /** i18n anahtarı — `backupDr.messages.*` */
  messageKey: string;
}

/**
 * Backend `NewQueuedRunCreated` / `DuplicateExecutionPrevented` bayraklarına göre birincil geri bildirim.
 * İkisi birden true gibi çelişkili yanıtlarda nötr bilgi mesajı.
 */
export function describeBackupTriggerOutcome(res: BackupTriggerResponseDto): BackupTriggerFeedback {
  const dup = res.duplicateExecutionPrevented === true;
  const queued = res.newQueuedRunCreated === true;

  if (dup && queued) {
    return { level: 'info', messageKey: 'backupDr.messages.backupTriggerAmbiguous' };
  }
  if (dup) {
    return { level: 'info', messageKey: 'backupDr.messages.backupDuplicateActive' };
  }
  if (queued) {
    return { level: 'success', messageKey: 'backupDr.messages.backupNewQueued' };
  }
  return { level: 'info', messageKey: 'backupDr.messages.backupIdempotentReplay' };
}
