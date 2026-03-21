import { jwtDecode } from 'jwt-decode';

import { storage } from './storage';

const NAME_ID_CLAIM =
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';

/**
 * Resolves cashierId for POST /api/pos/payment. Backend rejects mismatches vs JWT (CASHIER_ID_MISMATCH).
 * Prefer JWT claims over AuthContext user (can be null or stale during refresh).
 */
export async function resolveCashierIdForPayment(
  authContextUserId?: string | null
): Promise<string> {
  const raw = await storage.getItem('token');
  if (!raw) {
    const u = authContextUserId?.trim();
    return u || 'UNKNOWN';
  }
  const token = raw.startsWith('Bearer ') ? raw.slice(7) : raw;
  try {
    const d = jwtDecode<Record<string, unknown>>(token);
    const candidates = [d.sub, d.user_id, d[NAME_ID_CLAIM], d.nameid];
    for (const c of candidates) {
      if (typeof c === 'string' && c.trim()) return c.trim();
    }
  } catch {
    // ignore decode errors
  }
  const u = authContextUserId?.trim();
  return u || 'UNKNOWN';
}
