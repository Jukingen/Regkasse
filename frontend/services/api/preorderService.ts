import { apiClient } from './config';

export type PreorderStatus = 'pending' | 'ready' | 'collected' | 'cancelled';

export type PreorderDto = {
  id: string;
  orderId: string;
  receiptNumber?: string | null;
  preorderNumber?: string | null;
  sourcePaymentId?: string | null;
  status: PreorderStatus;
  customerName?: string | null;
  customerNotes?: string | null;
  totalAmount: number;
  paidAmount?: number;
  remainingAmount?: number;
  pickupDeadlineUtc?: string | null;
  pickupWeeks?: number;
  orderDate: string;
  readyAtUtc?: string | null;
  collectedAtUtc?: string | null;
};

export type PreorderListResponse = {
  pending: number;
  ready: number;
  collected: number;
  cancelled: number;
  orders: PreorderDto[];
};

function readString(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function mapPreorder(raw: Record<string, unknown>): PreorderDto {
  return {
    id: readString(raw.id ?? raw.Id),
    orderId: readString(raw.orderId ?? raw.OrderId),
    receiptNumber: (raw.receiptNumber ?? raw.ReceiptNumber) as string | null,
    preorderNumber: (raw.preorderNumber ?? raw.PreorderNumber) as string | null,
    sourcePaymentId: (raw.sourcePaymentId ?? raw.SourcePaymentId) as string | null,
    status: readString(raw.status ?? raw.Status).toLowerCase() as PreorderStatus,
    customerName: (raw.customerName ?? raw.CustomerName) as string | null,
    customerNotes: (raw.customerNotes ?? raw.CustomerNotes) as string | null,
    totalAmount: Number(raw.totalAmount ?? raw.TotalAmount ?? 0),
    paidAmount: Number(raw.paidAmount ?? raw.PaidAmount ?? raw.totalAmount ?? raw.TotalAmount ?? 0),
    remainingAmount: Number(raw.remainingAmount ?? raw.RemainingAmount ?? 0),
    pickupDeadlineUtc: (raw.pickupDeadlineUtc ?? raw.PickupDeadlineUtc) as string | null,
    pickupWeeks: Number(raw.pickupWeeks ?? raw.PickupWeeks ?? 0),
    orderDate: readString(raw.orderDate ?? raw.OrderDate),
    readyAtUtc: (raw.readyAtUtc ?? raw.ReadyAtUtc) as string | null,
    collectedAtUtc: (raw.collectedAtUtc ?? raw.CollectedAtUtc) as string | null,
  };
}

function mapList(raw: unknown): PreorderListResponse {
  const rec = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const rows = (rec.orders ?? rec.Orders ?? []) as Record<string, unknown>[];
  return {
    pending: Number(rec.pending ?? rec.Pending ?? 0),
    ready: Number(rec.ready ?? rec.Ready ?? 0),
    collected: Number(rec.collected ?? rec.Collected ?? 0),
    cancelled: Number(rec.cancelled ?? rec.Cancelled ?? 0),
    orders: Array.isArray(rows) ? rows.map(mapPreorder) : [],
  };
}

export async function listPreorders(params?: {
  status?: PreorderStatus | '';
  receiptNumber?: string;
  take?: number;
}): Promise<PreorderListResponse> {
  const query = new URLSearchParams();
  if (params?.status) query.set('status', params.status);
  if (params?.receiptNumber?.trim()) query.set('receiptNumber', params.receiptNumber.trim());
  if (params?.take) query.set('take', String(params.take));
  const qs = query.toString();
  const data = await apiClient.get<unknown>(`/pos/orders/preorders${qs ? `?${qs}` : ''}`);
  return mapList(data);
}

export async function getPreorderByReceipt(receiptNumber: string): Promise<PreorderDto> {
  const key = encodeURIComponent(receiptNumber.trim());
  const data = await apiClient.get<unknown>(`/pos/orders/preorders/by-receipt/${key}`);
  return mapPreorder((data && typeof data === 'object' ? data : {}) as Record<string, unknown>);
}

export async function updatePreorderStatus(
  id: string,
  status: PreorderStatus
): Promise<PreorderDto> {
  const data = await apiClient.put<unknown>(`/pos/orders/${id}/preorder-status`, { status });
  return mapPreorder((data && typeof data === 'object' ? data : {}) as Record<string, unknown>);
}
