import { describe, expect, it } from 'vitest';

import {
  adminUserDisplayEmail,
  sameRole,
  sameTenantId,
} from '@/features/users/utils/adminUserRowDisplay';

describe('adminUserRowDisplay', () => {
  it('keeps a stored email and fills username@mandant when the address is missing', () => {
    expect(adminUserDisplayEmail('cashier1@dev.regkasse.at', 'cashier1', 'dev')).toBe(
      'cashier1@dev.regkasse.at'
    );
    expect(adminUserDisplayEmail('', 'manager1', 'prod')).toBe('manager1@prod.regkasse.at');
    expect(adminUserDisplayEmail(null, 'demo@demo.com', 'dev')).toBe('demo@demo.com');
  });

  it('matches tenant ids and roles without case differences', () => {
    expect(sameTenantId('ABC', 'abc')).toBe(true);
    expect(sameTenantId('abc', 'def')).toBe(false);
    expect(sameRole('Manager', 'manager')).toBe(true);
    expect(sameRole('Cashier', 'Kitchen')).toBe(false);
  });
});
