import { describe, expect, it } from '@jest/globals';

import { isPosAllowedRole } from '../utils/posRoleGuard';

describe('isPosAllowedRole', () => {
  it('allows Cashier, Waiter and SuperAdmin', () => {
    expect(isPosAllowedRole('Cashier')).toBe(true);
    expect(isPosAllowedRole('Waiter')).toBe(true);
    expect(isPosAllowedRole('SuperAdmin')).toBe(true);
  });

  it('denies Manager, Kitchen and missing role', () => {
    expect(isPosAllowedRole('Manager')).toBe(false);
    expect(isPosAllowedRole('Kitchen')).toBe(false);
    expect(isPosAllowedRole(null)).toBe(false);
    expect(isPosAllowedRole(undefined)).toBe(false);
  });

  it('allows when Waiter is only in roles[]', () => {
    expect(isPosAllowedRole('Kitchen', ['Waiter'])).toBe(true);
  });

  it('matches roles case-insensitively', () => {
    expect(isPosAllowedRole('cashier')).toBe(true);
    expect(isPosAllowedRole('CASHIER')).toBe(true);
  });
});
