import { describe, expect, it } from 'vitest';

import { manualRestoreSuccessMessageKey } from '@/features/backup/logic/manualRestoreSuccessMessage';

describe('manualRestoreSuccessMessageKey', () => {
  it('uses executing message when restore auto-started (Development Super Admin)', () => {
    expect(manualRestoreSuccessMessageKey('Executing')).toBe(
      'backupDr.manualRestore.messages.requestExecuting'
    );
    expect(manualRestoreSuccessMessageKey('Approved')).toBe(
      'backupDr.manualRestore.messages.requestExecuting'
    );
  });

  it('uses approval-required message for pending requests (Production/Staging)', () => {
    expect(manualRestoreSuccessMessageKey('PendingApproval')).toBe(
      'backupDr.manualRestore.messages.requestCreated'
    );
    expect(manualRestoreSuccessMessageKey(undefined)).toBe(
      'backupDr.manualRestore.messages.requestCreated'
    );
  });
});
