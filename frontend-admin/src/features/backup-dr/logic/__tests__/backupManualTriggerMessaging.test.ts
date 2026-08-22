import { describe, expect, it } from 'vitest';

import { triggerErrorMessageBackupDashboard } from '../backupManualTriggerMessaging';

const t = (key: string, options?: Record<string, string | number>) =>
  options ? `${key}|${options.limit}|${options.current}` : key;

describe('triggerErrorMessageBackupDashboard', () => {
  it('maps LIMIT_EXCEEDED count cap to localized backup copy', () => {
    const err = {
      isAxiosError: true,
      response: {
        status: 409,
        data: {
          code: 'LIMIT_EXCEEDED',
          limitKey: 'maxBackupsPerTenant',
          limit: 50,
          current: 50,
          message: 'Maximum 50 backups per tenant reached',
        },
      },
    };

    expect(triggerErrorMessageBackupDashboard(err, t)).toBe(
      'tenants.limits.errors.maxBackupsPerTenant|50|50'
    );
  });

  it('maps LIMIT_EXCEEDED size cap to localized backup copy', () => {
    const err = {
      isAxiosError: true,
      response: {
        status: 409,
        data: {
          code: 'LIMIT_EXCEEDED',
          limitKey: 'maxBackupSizeMB',
          limit: 500,
          current: 512,
        },
      },
    };

    expect(triggerErrorMessageBackupDashboard(err, t)).toBe(
      'tenants.limits.errors.maxBackupSizeMB|500|512'
    );
  });

  it('keeps generic 409 copy when the conflict is not a tenant limit', () => {
    const err = {
      isAxiosError: true,
      response: { status: 409, data: { code: 'BACKUP_RUN_NOT_SUCCEEDED' } },
    };

    expect(triggerErrorMessageBackupDashboard(err, t)).toBe('backupDr.errors.conflictTrigger');
  });
});
