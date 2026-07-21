import { describe, expect, it } from 'vitest';

import { buildFinanzOnlineOutboxHandoffHref } from '@/shared/investigationNavigation';

describe('buildFinanzOnlineOutboxHandoffHref', () => {
  it('returns deep link with outboxId for valid UUID (authoritative outbox handoff)', () => {
    const id = '11111111-1111-4111-8111-111111111111';
    expect(buildFinanzOnlineOutboxHandoffHref(id)).toBe(
      `/rksv/finanz-online-outbox?outboxId=${encodeURIComponent(id)}`
    );
  });

  it('returns null for invalid or empty ids (never poison URLs)', () => {
    expect(buildFinanzOnlineOutboxHandoffHref(null)).toBeNull();
    expect(buildFinanzOnlineOutboxHandoffHref('')).toBeNull();
    expect(buildFinanzOnlineOutboxHandoffHref('not-a-uuid')).toBeNull();
  });
});
