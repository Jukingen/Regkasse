import { apiClient } from './config';

export type KitchenOrderStatus =
  | 'Pending'
  | 'InPreparation'
  | 'Ready'
  | 'Served'
  | 'Cancelled';

export type KitchenOrderItemStatus = 'Pending' | 'Preparing' | 'Ready' | 'Served';

export interface KitchenOrderItem {
  id: string;
  productId?: string | null;
  productName: string;
  quantity: number;
  notes?: string | null;
  status: KitchenOrderItemStatus;
}

export interface KitchenOrder {
  id: string;
  cartId?: string | null;
  tableNumber?: string | null;
  cashRegisterId: string;
  status: KitchenOrderStatus;
  priority: number;
  notes?: string | null;
  createdAtUtc: string;
  items: KitchenOrderItem[];
}

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function mapItem(value: unknown): KitchenOrderItem {
  const raw = asRecord(value);
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    productId: (raw.productId ?? raw.ProductId ?? null) as string | null,
    productName: String(raw.productName ?? raw.ProductName ?? ''),
    quantity: Number(raw.quantity ?? raw.Quantity ?? 0),
    notes: (raw.notes ?? raw.Notes ?? null) as string | null,
    status: String(raw.status ?? raw.Status ?? 'Pending') as KitchenOrderItemStatus,
  };
}

export function mapKitchenOrder(value: unknown): KitchenOrder {
  const raw = asRecord(value);
  const itemsRaw = raw.items ?? raw.Items;
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    cartId: (raw.cartId ?? raw.CartId ?? null) as string | null,
    tableNumber: (raw.tableNumber ?? raw.TableNumber ?? null) as string | null,
    cashRegisterId: String(raw.cashRegisterId ?? raw.CashRegisterId ?? ''),
    status: String(raw.status ?? raw.Status ?? 'Pending') as KitchenOrderStatus,
    priority: Number(raw.priority ?? raw.Priority ?? 0),
    notes: (raw.notes ?? raw.Notes ?? null) as string | null,
    createdAtUtc: String(raw.createdAtUtc ?? raw.CreatedAtUtc ?? ''),
    items: Array.isArray(itemsRaw) ? itemsRaw.map(mapItem) : [],
  };
}

export async function createKitchenOrder(payload: {
  cartId?: string | null;
  tableNumber?: string | null;
  cashRegisterId: string;
  notes?: string | null;
  items: Array<{ productId?: string; productName: string; quantity: number; notes?: string }>;
}): Promise<KitchenOrder> {
  const response = await apiClient.post<unknown>('/pos/kitchen-orders', payload);
  return mapKitchenOrder(response);
}

export async function listKitchenOrders(params?: {
  status?: KitchenOrderStatus;
  from?: string;
  to?: string;
}): Promise<KitchenOrder[]> {
  const body = await apiClient.get<unknown>('/pos/kitchen-orders', { params });
  const items = asRecord(body).items;
  const rows = Array.isArray(body) ? body : Array.isArray(items) ? items : [];
  return rows.map(mapKitchenOrder);
}

export async function updateKitchenOrderStatus(
  id: string,
  status: KitchenOrderStatus
): Promise<KitchenOrder> {
  const response = await apiClient.patch<unknown>(`/pos/kitchen-orders/${encodeURIComponent(id)}/status`, {
    status,
  });
  return mapKitchenOrder(response);
}

export async function updateKitchenOrderItemStatus(
  id: string,
  itemId: string,
  status: KitchenOrderItemStatus
): Promise<KitchenOrder> {
  const response = await apiClient.patch<unknown>(
    `/pos/kitchen-orders/${encodeURIComponent(id)}/items/${encodeURIComponent(itemId)}/status`,
    { status }
  );
  return mapKitchenOrder(response);
}
