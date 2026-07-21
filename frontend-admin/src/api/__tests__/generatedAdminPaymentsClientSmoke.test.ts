import { describe, expect, it } from 'vitest';

import {
  getApiAdminPayments,
  getApiAdminPaymentsId,
  getApiAdminPaymentsIdReprintPdf,
  getApiAdminPaymentsPaymentIdReprint,
} from '@/api/generated/admin/admin';

/**
 * Smoke: Orval output must export payment list/detail clients used by Admin.
 * Catches accidental renames or removal when OpenAPI/tags change.
 */
describe('generated admin payments client (OpenAPI / Orval)', () => {
  it('exports list and detail request functions', () => {
    expect(typeof getApiAdminPayments).toBe('function');
    expect(typeof getApiAdminPaymentsId).toBe('function');
    expect(typeof getApiAdminPaymentsIdReprintPdf).toBe('function');
    expect(typeof getApiAdminPaymentsPaymentIdReprint).toBe('function');
  });
});
