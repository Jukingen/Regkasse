/**
 * Local queue for payment attempts that never reached the server (transport failure).
 * Entries are NON-FISCAL until POST /Payment succeeds and isSynced becomes true.
 */
import { apiClient } from '../api/config';
import { storage } from '../../utils/storage';

const STORAGE_KEY = '@regkasse/pending_payments_v1';

/** Same payload as POST /Payment (avoids circular import with paymentService). */
export interface PendingPaymentPayload {
  customerId: string;
  items: { productId: string; quantity: number; taxType: string }[];
  payment: { method: string; tseRequired: boolean; amount?: number };
  tableNumber: number;
  cashierId: string;
  totalAmount: number;
  cashRegisterId: string;
  steuernummer?: string;
  notes?: string;
  idempotencyKey?: string;
}

export interface PendingPaymentEntry {
  /** Client-side queue row id */
  queueId: string;
  paymentRequest: PendingPaymentPayload;
  createdAt: string;
  /** false until server accepted POST /Payment */
  isSynced: boolean;
  lastAttemptAt?: string;
  lastError?: string;
}

async function readQueue(): Promise<PendingPaymentEntry[]> {
  const raw = await storage.getItem(STORAGE_KEY);
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

async function writeQueue(entries: PendingPaymentEntry[]): Promise<void> {
  await storage.setItem(STORAGE_KEY, JSON.stringify(entries));
}

function newQueueId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

/**
 * Append payment to pending queue (or return existing queue id for same idempotency key).
 */
export async function enqueuePendingPayment(
  paymentRequest: PendingPaymentPayload
): Promise<string> {
  const q = await readQueue();
  const idem = paymentRequest.idempotencyKey?.trim();
  if (idem) {
    const existing = q.find(
      (e) => !e.isSynced && e.paymentRequest.idempotencyKey === idem
    );
    if (existing) return existing.queueId;
  }
  const entry: PendingPaymentEntry = {
    queueId: newQueueId(),
    paymentRequest: { ...paymentRequest },
    createdAt: new Date().toISOString(),
    isSynced: false,
  };
  q.push(entry);
  await writeQueue(q);
  return entry.queueId;
}

export async function getPendingPaymentQueue(): Promise<PendingPaymentEntry[]> {
  const q = await readQueue();
  return q.filter((e) => !e.isSynced);
}

export async function removePendingByQueueId(queueId: string): Promise<void> {
  const q = await readQueue();
  await writeQueue(q.filter((e) => e.queueId !== queueId));
}

export async function removePendingByIdempotencyKey(key: string): Promise<void> {
  if (!key?.trim()) return;
  const q = await readQueue();
  await writeQueue(q.filter((e) => e.paymentRequest.idempotencyKey !== key));
}

async function touchAttempt(queueId: string, err: string): Promise<void> {
  const q = await readQueue();
  const e = q.find((x) => x.queueId === queueId);
  if (e) {
    e.lastAttemptAt = new Date().toISOString();
    e.lastError = err;
    await writeQueue(q);
  }
}

function isPostPaymentSuccess(raw: unknown): boolean {
  if (!raw || typeof raw !== 'object') return false;
  const r = raw as Record<string, unknown>;
  const inner = (r.Value ?? r) as Record<string, unknown>;
  return (
    inner.success === true ||
    inner.Success === true ||
    (inner.data as Record<string, unknown>)?.success === true ||
    (inner.data as Record<string, unknown>)?.Success === true
  );
}

/**
 * Retry POST /Payment for each unsynced entry. Removes row on server success.
 */
export async function syncPendingPaymentQueue(): Promise<{
  processed: number;
  failed: number;
}> {
  const pending = await getPendingPaymentQueue();
  let processed = 0;
  let failed = 0;

  for (const entry of pending) {
    try {
      const raw = await apiClient.post<unknown>('/Payment', entry.paymentRequest);
      if (isPostPaymentSuccess(raw)) {
        await removePendingByQueueId(entry.queueId);
        processed++;
      } else {
        failed++;
        await touchAttempt(entry.queueId, 'Server rejected payment');
      }
    } catch (e) {
      failed++;
      await touchAttempt(
        entry.queueId,
        e instanceof Error ? e.message : 'sync_failed'
      );
    }
  }

  return { processed, failed };
}
