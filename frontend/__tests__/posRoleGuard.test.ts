import { describe, expect, it } from '@jest/globals';

import { isPosAllowedRole } from '../utils/posRoleGuard';

describe('isPosAllowedRole', () => {
  it('allows Cashier, Waiter, Kitchen and SuperAdmin', () => {
    expect(isPosAllowedRole('Cashier')).toBe(true);
    expect(isPosAllowedRole('Waiter')).toBe(true);
    expect(isPosAllowedRole('Kitchen')).toBe(true);
    expect(isPosAllowedRole('SuperAdmin')).toBe(true);
  });

  it('denies Manager and missing role', () => {
    expect(isPosAllowedRole('Manager')).toBe(false);
    expect(isPosAllowedRole(null)).toBe(false);
    expect(isPosAllowedRole(undefined)).toBe(false);
  });

  it('allows when Waiter is only in roles[]', () => {
    expect(isPosAllowedRole('Manager', ['Waiter'])).toBe(true);
  });

  it('matches roles case-insensitively', () => {
    expect(isPosAllowedRole('cashier')).toBe(true);
    expect(isPosAllowedRole('CASHIER')).toBe(true);
  });
});
