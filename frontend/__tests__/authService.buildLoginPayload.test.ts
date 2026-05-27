import { buildLoginPayload } from '../services/api/loginPayload';

describe('buildLoginPayload', () => {
  it('sends loginIdentifier, legacy email mirror, password, and clientApp pos', () => {
    const payload = buildLoginPayload('cashier1', 'Secret123!', 'pos');

    expect(payload).toEqual({
      loginIdentifier: 'cashier1',
      email: 'cashier1',
      password: 'Secret123!',
      clientApp: 'pos',
    });
  });

  it('trims whitespace from loginIdentifier', () => {
    const payload = buildLoginPayload('  cashier1  ', 'pass', 'pos');

    expect(payload.loginIdentifier).toBe('cashier1');
    expect(payload.email).toBe('cashier1');
  });

  it('supports email-style identifiers', () => {
    const payload = buildLoginPayload('cashier@cafe.regkasse.at', 'pass', 'pos');

    expect(payload.loginIdentifier).toBe('cashier@cafe.regkasse.at');
    expect(payload.clientApp).toBe('pos');
  });
});
