import { describe, expect, it } from 'vitest';

/**
 * Smoke: Orval exports outbox list/detail used by RKSV FinanzOnline · Outbox screen.
 * Dynamic import after env: axios mutator loads at module init.
 */
describe('generated admin FinanzOnline outbox client (OpenAPI / Orval)', () => {
  it('exports list and detail request functions', async () => {
    process.env.NEXT_PUBLIC_API_BASE_URL =
      process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:5184';
    const { getApiAdminFinanzonlineOutbox, getApiAdminFinanzonlineOutboxId } =
      await import('@/api/generated/admin/admin');
    expect(typeof getApiAdminFinanzonlineOutbox).toBe('function');
    expect(typeof getApiAdminFinanzonlineOutboxId).toBe('function');
  });
});
