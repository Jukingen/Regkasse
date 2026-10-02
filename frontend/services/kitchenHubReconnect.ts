export const KITCHEN_HUB_PATH = '/hubs/kitchen';
export const KITCHEN_HUB_CREATED = 'KitchenOrderCreated';
export const KITCHEN_HUB_STATUS_CHANGED = 'KitchenOrderStatusChanged';
export const KITCHEN_HUB_ITEM_STATUS_CHANGED = 'KitchenOrderItemStatusChanged';

export const KITCHEN_HUB_RECONNECT_DELAYS_MS = [0, 2000, 5000, 10_000, 20_000] as const;
export const KITCHEN_HUB_RECONNECT_MAX_ELAPSED_MS = 120_000;

export const kitchenHubReconnectPolicy = {
  nextRetryDelayInMilliseconds(retryContext: {
    previousRetryCount: number;
    elapsedMilliseconds: number;
  }): number | null {
    if (retryContext.elapsedMilliseconds >= KITCHEN_HUB_RECONNECT_MAX_ELAPSED_MS) {
      return null;
    }
    if (retryContext.previousRetryCount >= KITCHEN_HUB_RECONNECT_DELAYS_MS.length) {
      return null;
    }
    return KITCHEN_HUB_RECONNECT_DELAYS_MS[retryContext.previousRetryCount] ?? null;
  },
};

export function resolveKitchenHubUrl(apiBaseUrl: string): string {
  const trimmed = apiBaseUrl.trim().replace(/\/+$/, '');
  const withoutApi = trimmed.replace(/\/api$/i, '');
  return `${withoutApi}${KITCHEN_HUB_PATH}`;
}
