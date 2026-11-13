import { describe, expect, it } from 'vitest';

import { ROLES, ROLE_HIERARCHY, hasMinRole } from '@/constants/roles';

describe('roles constants', () => {
  it('exposes backend-aligned role strings', () => {
    expect(ROLES.SUPER_ADMIN).toBe('SuperAdmin');
    expect(ROLES.MANAGER).toBe('Manager');
    expect(ROLES.CASHIER).toBe('Cashier');
  });

  it('orders hierarchy SuperAdmin > Manager > Cashier', () => {
    expect(ROLE_HIERARCHY[ROLES.SUPER_ADMIN]).toBeGreaterThan(ROLE_HIERARCHY[ROLES.MANAGER]);
    expect(ROLE_HIERARCHY[ROLES.MANAGER]).toBeGreaterThan(ROLE_HIERARCHY[ROLES.CASHIER]);
  });
});

describe('hasMinRole', () => {
  it('returns false when user role is missing', () => {
    expect(hasMinRole(undefined, ROLES.MANAGER)).toBe(false);
  });

  it('grants SuperAdmin for any defined minimum', () => {
    expect(hasMinRole(ROLES.SUPER_ADMIN, ROLES.SUPER_ADMIN)).toBe(true);
    expect(hasMinRole(ROLES.SUPER_ADMIN, ROLES.MANAGER)).toBe(true);
    expect(hasMinRole(ROLES.SUPER_ADMIN, ROLES.CASHIER)).toBe(true);
  });

  it('grants Manager for Manager and Cashier minimums only', () => {
    expect(hasMinRole(ROLES.MANAGER, ROLES.SUPER_ADMIN)).toBe(false);
    expect(hasMinRole(ROLES.MANAGER, ROLES.MANAGER)).toBe(true);
    expect(hasMinRole(ROLES.MANAGER, ROLES.CASHIER)).toBe(true);
  });

  it('grants Cashier only for Cashier minimum', () => {
    expect(hasMinRole(ROLES.CASHIER, ROLES.MANAGER)).toBe(false);
    expect(hasMinRole(ROLES.CASHIER, ROLES.CASHIER)).toBe(true);
  });

  it('treats unknown roles as level 0', () => {
    expect(hasMinRole('Waiter', ROLES.CASHIER)).toBe(false);
    expect(hasMinRole(ROLES.SUPER_ADMIN, 'UnknownRole')).toBe(true);
  });
});
