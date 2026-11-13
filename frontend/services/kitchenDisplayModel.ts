import type { KitchenOrder, KitchenOrderItemStatus, KitchenOrderStatus } from './api/kitchenOrderService';

export const KDS_ACTIVE_STATUSES: KitchenOrderStatus[] = ['Pending', 'InPreparation', 'Ready'];
export const KDS_OVERDUE_MS = 15 * 60 * 1000;

export function isKdsActiveStatus(status: KitchenOrderStatus): boolean {
  return KDS_ACTIVE_STATUSES.includes(status);
}

export function formatKitchenOrderAge(
  createdAtUtc: string,
  nowMs = Date.now()
): { text: string; overdue: boolean; totalSeconds: number } {
  const created = Date.parse(createdAtUtc);
  const elapsed = Number.isFinite(created) ? Math.max(0, nowMs - created) : 0;
  const totalSeconds = Math.floor(elapsed / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return {
    text: `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`,
    overdue: elapsed >= KDS_OVERDUE_MS,
    totalSeconds,
  };
}

export function nextKitchenItemStatus(
  status: KitchenOrderItemStatus
): KitchenOrderItemStatus | null {
  if (status === 'Pending') return 'Preparing';
  if (status === 'Preparing') return 'Ready';
  return null;
}

export function applyKitchenOrderEvent(
  orders: KitchenOrder[],
  order: KitchenOrder
): KitchenOrder[] {
  const without = orders.filter((row) => row.id !== order.id);
  if (!isKdsActiveStatus(order.status)) {
    return without;
  }
  return [...without, order].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
}

export function countPendingKitchenOrders(orders: Array<{ status: KitchenOrderStatus }>): number {
  return orders.filter((row) => row.status === 'Pending').length;
}

export function ordersInColumn(
  orders: KitchenOrder[],
  status: KitchenOrderStatus
): KitchenOrder[] {
  return orders.filter((row) => row.status === status);
}
