import { isValidPosCashRegisterId } from '../utils/posCashRegister';

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
    expect(isValidPosCashRegisterId('00000000-0000-0000-0000-000000000000'.toUpperCase())).toBe(false);
  });
});
