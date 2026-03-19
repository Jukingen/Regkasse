import { describe, expect, it } from 'vitest';
import {
  normalizeCanClosePayload,
  normalizeCashRegisterListBody,
  normalizeTagesabschlussHistory,
  normalizeTagesabschlussStatistics,
} from './normalizers';

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

  it('normalizeTagesabschlussHistory keeps array of objects', () => {
    const rows = [{ closingId: 'c1', closingType: 'Daily', totalAmount: 10 }];
    expect(normalizeTagesabschlussHistory(rows)).toEqual(rows);
    expect(normalizeTagesabschlussHistory(null)).toEqual([]);
  });

  it('normalizeTagesabschlussStatistics returns object or null', () => {
    expect(normalizeTagesabschlussStatistics({ totalClosings: 2 })).toEqual({ totalClosings: 2 });
    expect(normalizeTagesabschlussStatistics(null)).toBeNull();
  });

  it('normalizeCanClosePayload returns undefined for non-objects', () => {
    expect(normalizeCanClosePayload(undefined)).toBeUndefined();
    expect(normalizeCanClosePayload({ canClose: true, message: 'ok' })).toEqual({
      canClose: true,
      message: 'ok',
    });
  });
});
