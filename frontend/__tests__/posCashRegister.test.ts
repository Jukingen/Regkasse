import {
  isValidPosCashRegisterId,
  needsPosCashRegisterSelection,
  resolveAutoOpenShiftRegisterId,
} from '../utils/posCashRegister';

describe('isValidPosCashRegisterId', () => {
  it('accepts non-empty non-zero GUID', () => {
    expect(isValidPosCashRegisterId('a4abaae3-2e6c-4e5c-97d4-c044d5ed21bb')).toBe(true);
  });

  it('rejects null, empty, whitespace', () => {
    expect(isValidPosCashRegisterId(null)).toBe(false);
    expect(isValidPosCashRegisterId(undefined)).toBe(false);
    expect(isValidPosCashRegisterId('')).toBe(false);
    expect(isValidPosCashRegisterId('   ')).toBe(false);
  });

  it('rejects empty GUID sentinel', () => {
    expect(isValidPosCashRegisterId('00000000-0000-0000-0000-000000000000')).toBe(false);
    expect(isValidPosCashRegisterId('00000000-0000-0000-0000-000000000000'.toUpperCase())).toBe(
      false
    );
  });

  it('rejects non-GUID strings that must not be POSTed as cashRegisterId', () => {
    expect(isValidPosCashRegisterId('KASSE-1')).toBe(false);
    expect(isValidPosCashRegisterId('not-a-guid')).toBe(false);
  });
});

describe('resolveAutoOpenShiftRegisterId', () => {
  it('returns trimmed GUID for auto-open', () => {
    expect(resolveAutoOpenShiftRegisterId('  a4abaae3-2e6c-4e5c-97d4-c044d5ed21bb  ')).toBe(
      'a4abaae3-2e6c-4e5c-97d4-c044d5ed21bb'
    );
  });

  it('returns null when no register is selected', () => {
    expect(resolveAutoOpenShiftRegisterId(undefined)).toBeNull();
    expect(resolveAutoOpenShiftRegisterId('')).toBeNull();
    expect(resolveAutoOpenShiftRegisterId('00000000-0000-0000-0000-000000000000')).toBeNull();
  });
});

describe('needsPosCashRegisterSelection', () => {
  it('is true without a selected register', () => {
    expect(needsPosCashRegisterSelection(null)).toBe(true);
    expect(needsPosCashRegisterSelection(undefined)).toBe(true);
  });

  it('is false when a valid register is assigned', () => {
    expect(needsPosCashRegisterSelection('a4abaae3-2e6c-4e5c-97d4-c044d5ed21bb')).toBe(false);
  });
});
