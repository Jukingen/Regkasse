import { describe, expect, it } from 'vitest';

import { normalizeCashRegisterListBody } from './normalizers';

describe('tagesabschluss normalizers', () => {
  it('normalizeCashRegisterListBody reads registers array', () => {
    expect(
      normalizeCashRegisterListBody({
        message: 'ok',
        registers: [{ id: 'a', registerNumber: '1', location: 'X' }],
      })
    ).toEqual([{ id: 'a', registerNumber: '1', location: 'X' }]);
  });

  it('normalizeCashRegisterListBody returns empty when registers missing', () => {
    expect(normalizeCashRegisterListBody({ message: 'ok' })).toEqual([]);
  });
});
