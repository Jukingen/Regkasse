import { describe, expect, it } from 'vitest';

import {
  restoreHistoryDisplayDate,
  restoreHistoryStatusLabelKey,
} from '@/features/backup/logic/restoreHistoryPresentation';

describe('restoreHistoryPresentation', () => {
  it('prefers approvedAt over requestedAt', () => {
    expect(
      restoreHistoryDisplayDate({
        requestId: '1',
        status: 'Completed',
        requestedAt: '2026-01-01T00:00:00Z',
        approvedAt: '2026-01-02T00:00:00Z',
        backupRunId: 'b',
        targetDatabaseName: 'restore_validation_x',
        validationOnly: true,
      })
    ).toBe('2026-01-02T00:00:00Z');
  });

  it('maps status to i18n key', () => {
    expect(restoreHistoryStatusLabelKey('PendingApproval')).toBe(
      'backupDr.manualRestore.status.pendingApproval'
    );
    expect(restoreHistoryStatusLabelKey('Completed')).toBe(
      'backupDr.manualRestore.status.completed'
    );
  });
});
