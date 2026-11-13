import { describe, expect, it } from 'vitest';

import { fiskalyStatusColor, readTagesabschlussFiskalyFields } from '../fiskalyFields';

describe('readTagesabschlussFiskalyFields', () => {
  it('reads camelCase Fiskaly fields from a history row', () => {
    expect(
      readTagesabschlussFiskalyFields({
        closingId: 'c1',
        fiskalyStatus: 'Submitted',
        fiskalyReceiptId: 'rx-1',
        fiskalyError: null,
      })
    ).toEqual({
      fiskalyStatus: 'Submitted',
      fiskalyReceiptId: 'rx-1',
      fiskalyError: null,
    });
  });

  it('returns empty fields for unknown input', () => {
    expect(readTagesabschlussFiskalyFields(null)).toEqual({});
    expect(fiskalyStatusColor('Submitted')).toBe('success');
    expect(fiskalyStatusColor('Failed')).toBe('error');
  });
});
