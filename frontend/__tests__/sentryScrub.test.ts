/// <reference types="jest" />

import { scrubPosSentryEvent } from '../src/monitoring/sentryScrub';

describe('scrubPosSentryEvent', () => {
  it('drops secret fields and keeps operator context', () => {
    const event = scrubPosSentryEvent({
      extra: {
        email: 'a@b.c',
        password: 'secret',
        token: 'jwt',
        voucherCode: 'GIFT',
        TseSignature: 'jws',
        userId: 'user-1',
        tenantId: 'tenant-1',
      },
      user: { id: 'user-1', email: 'a@b.c' },
      request: {
        method: 'POST',
        data: { password: 'secret', amount: 12 },
        headers: { token: 'jwt', 'content-type': 'application/json' },
      },
    });

    const extra = event.extra as Record<string, unknown>;
    expect(extra.email).toBeUndefined();
    expect(extra.password).toBeUndefined();
    expect(extra.token).toBeUndefined();
    expect(extra.voucherCode).toBeUndefined();
    expect(extra.TseSignature).toBeUndefined();
    expect(extra.userId).toBe('user-1');
    expect(extra.tenantId).toBe('tenant-1');
    expect((event.user as Record<string, unknown>).email).toBeUndefined();
    expect((event.user as Record<string, unknown>).id).toBe('user-1');
    const request = event.request as { data: Record<string, unknown>; headers: Record<string, unknown> };
    expect(request.data.password).toBeUndefined();
    expect(request.data.amount).toBe(12);
    expect(request.headers.token).toBeUndefined();
    expect(request.headers['content-type']).toBe('application/json');
  });
});
