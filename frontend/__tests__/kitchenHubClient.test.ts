import { describe, expect, it } from '@jest/globals';

import {
  KITCHEN_HUB_PATH,
  kitchenHubReconnectPolicy,
  resolveKitchenHubUrl,
} from '../services/kitchenHubReconnect';

describe('kitchen hub client', () => {
  it('resolves /hubs/kitchen from an /api base URL', () => {
    expect(resolveKitchenHubUrl('http://localhost:5184/api')).toBe(
      'http://localhost:5184/hubs/kitchen'
    );
    expect(KITCHEN_HUB_PATH).toBe('/hubs/kitchen');
  });

  it('reconnects with increasing delays then stops', () => {
    const ctx = (previousRetryCount: number, elapsedMilliseconds = 0) => ({
      previousRetryCount,
      elapsedMilliseconds,
      retryReason: new Error('test'),
    });

    expect(kitchenHubReconnectPolicy.nextRetryDelayInMilliseconds(ctx(0))).toBe(0);
    expect(kitchenHubReconnectPolicy.nextRetryDelayInMilliseconds(ctx(1))).toBe(2000);
    expect(kitchenHubReconnectPolicy.nextRetryDelayInMilliseconds(ctx(2))).toBe(5000);
    expect(kitchenHubReconnectPolicy.nextRetryDelayInMilliseconds(ctx(4))).toBe(20_000);
    expect(kitchenHubReconnectPolicy.nextRetryDelayInMilliseconds(ctx(5))).toBeNull();
    expect(
      kitchenHubReconnectPolicy.nextRetryDelayInMilliseconds(ctx(1, 120_000))
    ).toBeNull();
  });
});
