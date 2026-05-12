/**
 * Admin payments API helpers (manual boundary when callers prefer a stable module path).
 * Orval generates `getApiAdminPaymentsPaymentIdReprint`; this wrapper keeps auth via `customInstance`.
 */
import { getApiAdminPaymentsPaymentIdReprint } from '@/api/generated/admin/admin';

export async function reprintReceipt(paymentId: string, signal?: AbortSignal): Promise<Blob> {
  const data = await getApiAdminPaymentsPaymentIdReprint(paymentId, undefined, signal);
  if (data instanceof Blob) {
    return data;
  }
  return new Blob([data as BlobPart], { type: 'application/pdf' });
}
