import { describe, expect, it } from 'vitest';

import {
  getApiAdminOnlinePayments,
  getApiAdminOnlinePaymentsId,
  postApiAdminOnlinePaymentsTest,
} from '@/api/generated/admin/admin';

/**
 * Smoke: Orval output must export admin online-payment list/detail/test clients.
 */
describe('generated admin online-payments client (OpenAPI / Orval)', () => {
  it('exports list, detail, and test request functions', () => {
    expect(typeof getApiAdminOnlinePayments).toBe('function');
    expect(typeof getApiAdminOnlinePaymentsId).toBe('function');
    expect(typeof postApiAdminOnlinePaymentsTest).toBe('function');
  });
});
