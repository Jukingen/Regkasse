import {
  flattenUserSettingsPayload,
  readCashRegisterIdFromSettingsPayload,
  resolveUserSettingsRecord,
} from '../services/api/normalizeUserSettingsResponse';

describe('normalizeUserSettingsResponse', () => {
  it('reads cashRegisterId from flat payload (null in JSON becomes missing)', () => {
    const o = { userId: 'u1', id: 's1', cashRegisterId: null } as Record<string, unknown>;
    expect(readCashRegisterIdFromSettingsPayload(o)).toBeUndefined();
  });

  it('reads camelCase and PascalCase cash register id', () => {
    expect(
      readCashRegisterIdFromSettingsPayload({
        cashRegisterId: '  aaa  ',
      })
    ).toBe('aaa');
    expect(
      readCashRegisterIdFromSettingsPayload({
        CashRegisterId: 'bbb',
      })
    ).toBe('bbb');
  });

  it('unwraps SuccessResponse-like envelope to inner settings', () => {
    const inner = { userId: 'u', id: 'x', cashRegisterId: 'reg-1' };
    const wrapped = { success: true, data: inner, message: 'ok' };
    const flat = flattenUserSettingsPayload(wrapped);
    expect(readCashRegisterIdFromSettingsPayload(flat)).toBe('reg-1');
    const rec = resolveUserSettingsRecord(wrapped);
    expect(readCashRegisterIdFromSettingsPayload(rec)).toBe('reg-1');
  });

  it('resolveUserSettingsRecord falls back to top-level when inner empty', () => {
    const top = { userId: 'u', cashRegisterId: 'top-reg' };
    expect(readCashRegisterIdFromSettingsPayload(resolveUserSettingsRecord(top))).toBe('top-reg');
  });
});
