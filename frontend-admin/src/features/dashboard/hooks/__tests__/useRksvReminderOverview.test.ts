import { describe, expect, it } from 'vitest';

import type { RksvReminderStatusDto } from '@/api/generated/model';
import {
  isJahresbelegActionRequired,
  isStartbelegMissing,
} from '@/features/dashboard/hooks/useRksvReminderOverview';

describe('RKSV reminder status helpers', () => {
  it('detects missing Startbeleg', () => {
    const status = {
      startbeleg: { isRequired: true, status: 'missing' },
      monatsbeleg: { isRequired: false, status: 'ok' },
      jahresbeleg: { isRequired: false, status: 'ok' },
    } as RksvReminderStatusDto;
    expect(isStartbelegMissing(status)).toBe(true);
  });

  it('detects Jahresbeleg attention when required and overdue', () => {
    const status = {
      startbeleg: { isRequired: false, status: 'present' },
      monatsbeleg: { isRequired: false, status: 'ok' },
      jahresbeleg: { isRequired: true, status: 'overdue', daysUntilDeadline: 0 },
    } as RksvReminderStatusDto;
    expect(isJahresbelegActionRequired(status)).toBe(true);
  });
});
