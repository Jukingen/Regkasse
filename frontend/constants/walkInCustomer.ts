import { apiClient } from '../services/api/config';

export const WALK_IN_CUSTOMER_ID_FALLBACK = '00000000-0000-0000-0000-000000000001';

export function isWalkInCustomerId(id: string | null | undefined): boolean {
  const t = (id ?? '').trim().toLowerCase();
  return t === WALK_IN_CUSTOMER_ID_FALLBACK.toLowerCase();
}

let cachedWalkInId: string | null = null;

export async function resolveWalkInCustomerId(): Promise<string> {
  if (cachedWalkInId) return cachedWalkInId;
  try {
    const raw = await apiClient.get<unknown>('/Customer/walk-in');
    const layer = raw as Record<string, unknown>;
    const data = (layer?.data ?? layer) as Record<string, unknown>;
    const id = (data?.customerId ?? data?.CustomerId) as string | undefined;
    if (typeof id === 'string' && id.trim()) {
      cachedWalkInId = id.trim();
      return cachedWalkInId;
    }
  } catch {
    /* use fallback */
  }
  return WALK_IN_CUSTOMER_ID_FALLBACK;
}
