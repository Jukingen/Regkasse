import { describe, expect, it } from 'vitest';

import {
  isDepExportCompleted,
  isDepExportHistoryCompleted,
  normalizeDepExportHistoryStatus,
} from '@/features/rksv/hooks/useDepExportHistory';

describe('dep export history status helpers', () => {
  it('treats string and numeric Completed as completed', () => {
    expect(isDepExportCompleted('Completed')).toBe(true);
    expect(isDepExportCompleted(2)).toBe(true);
    expect(isDepExportCompleted('2')).toBe(true);
    expect(isDepExportHistoryCompleted(2)).toBe(true);
    expect(isDepExportCompleted('Failed')).toBe(false);
    expect(isDepExportCompleted(3)).toBe(false);
  });

  it('normalizes legacy numeric statuses', () => {
    expect(normalizeDepExportHistoryStatus(0)).toBe('Pending');
    expect(normalizeDepExportHistoryStatus(1)).toBe('Processing');
    expect(normalizeDepExportHistoryStatus(2)).toBe('Completed');
    expect(normalizeDepExportHistoryStatus(3)).toBe('Failed');
    expect(normalizeDepExportHistoryStatus('Completed')).toBe('Completed');
  });
});
