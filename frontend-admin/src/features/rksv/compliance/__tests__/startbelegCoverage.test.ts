import { describe, expect, it } from 'vitest';
import { findRegistersMissingStartbeleg } from '@/features/rksv/compliance/startbelegCoverage';

describe('findRegistersMissingStartbeleg', () => {
  it('flags registers without a Startbeleg in special receipts', () => {
    const missing = findRegistersMissingStartbeleg(
      [
        { id: 'a', registerNumber: 'K1' },
        { id: 'b', registerNumber: 'K2' },
      ],
      [{ cashRegisterId: 'a', kind: 'Startbeleg' }],
    );
    expect(missing).toEqual([{ cashRegisterId: 'b', registerNumber: 'K2' }]);
  });

  it('scopes to one register when filter is set', () => {
    const missing = findRegistersMissingStartbeleg(
      [
        { id: 'a', registerNumber: 'K1' },
        { id: 'b', registerNumber: 'K2' },
      ],
      [],
      'b',
    );
    expect(missing).toEqual([{ cashRegisterId: 'b', registerNumber: 'K2' }]);
  });
});
