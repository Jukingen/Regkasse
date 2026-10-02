import { useSyncExternalStore } from 'react';

import type { KitchenOrder } from './api/kitchenOrderService';
import { countPendingKitchenOrders } from './kitchenDisplayModel';

let pendingIds = new Set<string>();
const listeners = new Set<() => void>();

function emit(): void {
  listeners.forEach((listener) => listener());
}

export function getKitchenPendingCount(): number {
  return pendingIds.size;
}

export function subscribeKitchenPendingCount(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function syncKitchenPendingFromOrders(orders: KitchenOrder[]): void {
  const next = new Set<string>();
  for (const order of orders) {
    if (order.status === 'Pending' && order.id) {
      next.add(order.id);
    }
  }
  if (next.size === pendingIds.size && [...next].every((id) => pendingIds.has(id))) {
    return;
  }
  pendingIds = next;
  emit();
}

export function applyKitchenPendingFromOrder(order: KitchenOrder): void {
  const next = new Set(pendingIds);
  if (order.status === 'Pending' && order.id) {
    next.add(order.id);
  } else {
    next.delete(order.id);
  }
  if (next.size === pendingIds.size && (order.status !== 'Pending' || pendingIds.has(order.id))) {
    if (order.status === 'Pending' && pendingIds.has(order.id)) return;
    if (order.status !== 'Pending' && !pendingIds.has(order.id)) return;
  }
  pendingIds = next;
  emit();
}

export function resetKitchenPendingStore(): void {
  if (pendingIds.size === 0) return;
  pendingIds = new Set();
  emit();
}

export function useKitchenPendingCount(): number {
  return useSyncExternalStore(
    subscribeKitchenPendingCount,
    getKitchenPendingCount,
    getKitchenPendingCount
  );
}

export { countPendingKitchenOrders };
