import type { RetryContext } from '@microsoft/signalr';
import { describe, expect, it } from 'vitest';

import { demoImportReconnectPolicy } from '@/features/tenants/api/demoImportJobs';

function ctx(partial: Partial<RetryContext>): RetryContext {
  return {
    previousRetryCount: 0,
    elapsedMilliseconds: 0,
    retryReason: new Error('test'),
    ...partial,
  };
}

describe('demoImportReconnectPolicy', () => {
  it('uses increasing delays then stops', () => {
    expect(
      demoImportReconnectPolicy.nextRetryDelayInMilliseconds(ctx({ previousRetryCount: 0 }))
    ).toBe(0);
    expect(
      demoImportReconnectPolicy.nextRetryDelayInMilliseconds(ctx({ previousRetryCount: 1 }))
    ).toBe(2000);
    expect(
      demoImportReconnectPolicy.nextRetryDelayInMilliseconds(ctx({ previousRetryCount: 2 }))
    ).toBe(5000);
    expect(
      demoImportReconnectPolicy.nextRetryDelayInMilliseconds(ctx({ previousRetryCount: 5 }))
    ).toBe(30000);
    expect(
      demoImportReconnectPolicy.nextRetryDelayInMilliseconds(ctx({ previousRetryCount: 6 }))
    ).toBeNull();
  });

  it('stops after max elapsed time even if retry count remains', () => {
    expect(
      demoImportReconnectPolicy.nextRetryDelayInMilliseconds(
        ctx({ previousRetryCount: 1, elapsedMilliseconds: 120_000 })
      )
    ).toBeNull();
  });
});
