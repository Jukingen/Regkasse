import { customInstance } from '@/lib/axios';

export type PreorderStatus = 'pending' | 'ready' | 'collected' | 'cancelled';

export type AdminPreorder = {
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
  orderDate: string;
  readyAtUtc?: string | null;
  collectedAtUtc?: string | null;
};

export type AdminPreorderSettings = {
  pickupDeadlineWeeks: number;
  cancellationPolicyText: string;
};

export type AdminPreorderListResponse = {
  pending: number;
  ready: number;
  collected: number;
  cancelled: number;
  orders: AdminPreorder[];
};

export type AdminPreorderStats = {
  pending: number;
  ready: number;
  collected: number;
  cancelled: number;
};

function mapRow(raw: Record<string, unknown>): AdminPreorder {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    orderId: String(raw.orderId ?? raw.OrderId ?? ''),
    receiptNumber: (raw.receiptNumber ?? raw.ReceiptNumber) as string | null,
    preorderNumber: (raw.preorderNumber ?? raw.PreorderNumber) as string | null,
    sourcePaymentId: (raw.sourcePaymentId ?? raw.SourcePaymentId) as string | null,
    status: String(raw.status ?? raw.Status ?? 'pending').toLowerCase() as PreorderStatus,
    customerName: (raw.customerName ?? raw.CustomerName) as string | null,
    customerNotes: (raw.customerNotes ?? raw.CustomerNotes) as string | null,
    totalAmount: Number(raw.totalAmount ?? raw.TotalAmount ?? 0),
    paidAmount: Number(raw.paidAmount ?? raw.PaidAmount ?? raw.totalAmount ?? raw.TotalAmount ?? 0),
    remainingAmount: Number(raw.remainingAmount ?? raw.RemainingAmount ?? 0),
    orderDate: String(raw.orderDate ?? raw.OrderDate ?? ''),
    readyAtUtc: (raw.readyAtUtc ?? raw.ReadyAtUtc) as string | null,
    collectedAtUtc: (raw.collectedAtUtc ?? raw.CollectedAtUtc) as string | null,
  };
}

function mapList(raw: unknown): AdminPreorderListResponse {
  const rec = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const rows = (rec.orders ?? rec.Orders ?? []) as Record<string, unknown>[];
  return {
    pending: Number(rec.pending ?? rec.Pending ?? 0),
    ready: Number(rec.ready ?? rec.Ready ?? 0),
    collected: Number(rec.collected ?? rec.Collected ?? 0),
    cancelled: Number(rec.cancelled ?? rec.Cancelled ?? 0),
    orders: Array.isArray(rows) ? rows.map(mapRow) : [],
  };
}

export async function fetchAdminPreorders(params?: {
  status?: string;
  receiptNumber?: string;
}): Promise<AdminPreorderListResponse> {
  const query: Record<string, unknown> = {};
  if (params?.status) query.status = params.status;
  if (params?.receiptNumber) query.receiptNumber = params.receiptNumber;
  const data = await customInstance<unknown>({
    url: '/api/admin/orders/preorders',
    method: 'GET',
    params: query,
  });
  return mapList(data);
}

export async function fetchAdminPreorderStats(): Promise<AdminPreorderStats> {
  const data = await customInstance<unknown>({
    url: '/api/admin/orders/preorder-stats',
    method: 'GET',
  });
  const rec = data && typeof data === 'object' ? (data as Record<string, unknown>) : {};
  return {
    pending: Number(rec.pending ?? rec.Pending ?? 0),
    ready: Number(rec.ready ?? rec.Ready ?? 0),
    collected: Number(rec.collected ?? rec.Collected ?? 0),
    cancelled: Number(rec.cancelled ?? rec.Cancelled ?? 0),
  };
}

export async function fetchAdminPreorderSettings(): Promise<AdminPreorderSettings> {
  const data = await customInstance<unknown>({
    url: '/api/admin/orders/preorder-settings',
    method: 'GET',
  });
  const rec = data && typeof data === 'object' ? (data as Record<string, unknown>) : {};
  return {
    pickupDeadlineWeeks: Number(rec.pickupDeadlineWeeks ?? rec.PickupDeadlineWeeks ?? 4),
    cancellationPolicyText: String(
      rec.cancellationPolicyText ??
        rec.CancellationPolicyText ??
        'Keine Rücknahme von Artikeln!'
    ),
  };
}

export async function updateAdminPreorderSettings(
  payload: AdminPreorderSettings
): Promise<AdminPreorderSettings> {
  const data = await customInstance<unknown>({
    url: '/api/admin/orders/preorder-settings',
    method: 'PUT',
    data: payload,
  });
  const rec = data && typeof data === 'object' ? (data as Record<string, unknown>) : {};
  return {
    pickupDeadlineWeeks: Number(rec.pickupDeadlineWeeks ?? rec.PickupDeadlineWeeks ?? payload.pickupDeadlineWeeks),
    cancellationPolicyText: String(
      rec.cancellationPolicyText ?? rec.CancellationPolicyText ?? payload.cancellationPolicyText
    ),
  };
}

export const adminPreorderQueryKeys = {
  list: (status?: string, receiptNumber?: string) =>
    ['admin-preorders', status ?? '', receiptNumber ?? ''] as const,
  stats: ['admin-preorder-stats'] as const,
  settings: ['admin-preorder-settings'] as const,
};
