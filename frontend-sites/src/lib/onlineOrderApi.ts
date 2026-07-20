import type { WebsiteStatus } from '@/lib/publicApi';
import { applyCsrfHeaders, ensureCsrfToken } from '@/lib/csrf';

function apiBase(): string {
  const raw = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5184';
  return raw.replace(/\/$/, '');
}

export type PlaceOnlineOrderItem = {
  productId: string;
  quantity: number;
};

export type PlaceOnlineOrderRequest = {
  tenant: string;
  customerName: string;
  customerPhone: string;
  customerEmail?: string;
  orderType?: 'takeaway' | 'delivery' | 'dine-in';
  deliveryAddress?: string;
  notes?: string;
  paymentMethod?: 'cash' | 'card' | 'online';
  source?: 'web' | 'pwa' | 'native';
  items: PlaceOnlineOrderItem[];
};

export type PlaceOnlineOrderResult = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  message?: string | null;
  orderId?: string | null;
  orderNumber?: string | null;
  total?: number | null;
  /** True when API rejected due to working hours (closed / cutoff). */
  closedByHours: boolean;
};

type ApiResponse = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  message?: string | null;
  Message?: string | null;
  orderId?: string | null;
  OrderId?: string | null;
  orderNumber?: string | null;
  OrderNumber?: string | null;
  total?: number | null;
  Total?: number | null;
};

/**
 * Place a website/app online order. Server enforces working hours (canOrder).
 * Never call this from POS/FA.
 */
export async function placeOnlineOrder(
  payload: PlaceOnlineOrderRequest,
): Promise<PlaceOnlineOrderResult> {
  try {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    try {
      const csrf = await ensureCsrfToken(apiBase());
      applyCsrfHeaders(headers, csrf);
    } catch {
      // Best-effort; Development / Csrf:Enabled=false skip enforcement.
    }

    const res = await fetch(`${apiBase()}/api/public/online-orders`, {
      method: 'POST',
      headers,
      credentials: 'include',
      cache: 'no-store',
      body: JSON.stringify(payload),
    });
    const dto = (await res.json().catch(() => null)) as ApiResponse | null;
    const code = dto?.code ?? dto?.Code ?? null;
    const succeeded = dto?.succeeded === true || dto?.Succeeded === true;
    return {
      succeeded,
      code,
      error: dto?.error ?? dto?.Error ?? null,
      message: dto?.message ?? dto?.Message ?? null,
      orderId: dto?.orderId ?? dto?.OrderId ?? null,
      orderNumber: dto?.orderNumber ?? dto?.OrderNumber ?? null,
      total: dto?.total ?? dto?.Total ?? null,
      closedByHours: code === 'ONLINE_ORDERS_CLOSED' || res.status === 409,
    };
  } catch {
    return {
      succeeded: false,
      code: 'NETWORK_ERROR',
      error: 'Bestellung konnte nicht gesendet werden.',
      closedByHours: false,
    };
  }
}

/** Re-export for callers that already imported status helpers from this module. */
export type { WebsiteStatus };

export function canPlaceOrders(status: WebsiteStatus | null | undefined): boolean {
  return status?.canOrder === true;
}
