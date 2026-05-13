/**
 * Controlled offline transaction queue (NON_FISCAL_PENDING -> Synced/Failed/Unknown).
 * Invariant: offline entries never contain receipt number / signature, or plaintext Gutschein codes.
 */
import { apiClient } from '../api/config';
import { storage } from '../../utils/storage';

const STORAGE_KEY = '@regkasse/offline_transactions_v1';
const LEGACY_STORAGE_KEY = '@regkasse/pending_payments_v1';
const DEVICE_ID_KEY = '@regkasse/device_id_v1';
const SEQUENCE_MAP_KEY = '@regkasse/client_sequence_map_v1';

/** Same payload as POST /api/pos/payment (avoids circular import with paymentService). */
export interface PendingPaymentPayload {
  customerId: string;
  items: { productId: string; quantity: number; taxType: string }[];
  payment: {
    method: string;
    tseRequired: boolean;
    amount?: number;
    voucherCode?: string;
    voucherRedemptions?: { code: string; amount: number }[];
  };
  tableNumber: number;
  totalAmount: number;
  cashRegisterId: string;
  steuernummer?: string;
  notes?: string;
  idempotencyKey?: string;
}

/** POS German copy when voucher must not be queued offline (security: no plaintext codes in AsyncStorage). */
export const VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE =
  'Gutschein-Zahlungen sind ohne Online-Verbindung nicht möglich. Bitte Internet prüfen und erneut versuchen. Aus Sicherheitsgründen wird der Gutscheincode nicht lokal zwischengespeichert.';

/** True if payload would carry plaintext voucher material (must never be written to the offline queue). */
export function paymentPayloadContainsVoucherSecrets(
  payment: PendingPaymentPayload['payment'] | undefined
): boolean {
  if (!payment) return false;
  if (typeof payment.voucherCode === 'string' && payment.voucherCode.trim().length > 0) return true;
  if (!Array.isArray(payment.voucherRedemptions) || payment.voucherRedemptions.length === 0) return false;
  return payment.voucherRedemptions.some(
    (r) => typeof r?.code === 'string' && r.code.trim().length > 0
  );
}

export type OfflineTransactionStatus = 'Pending' | 'Synced' | 'Failed' | 'Unknown';

export type ReplayOfflineTransactionsResponseItem = {
  requestedOfflineTransactionId: string;
  /** Server row id; if missing despite a matched request id, treat as incomplete response (do not mark Failed). */
  offlineTransactionId?: string | null;
  status: string;
  syncedPaymentId?: string | null;
  error?: string | null;
  errorCode?: string | null;
  retryCount?: number;
  lastErrorMessageSafe?: string | null;
  exponentialBackoffHintSeconds?: number | null;
  /** Same for all items in one replay batch; for support/audit correlation. */
  replayBatchCorrelationId?: string | null;
};

export type ReplayOfflineTransactionsResponse = {
  success: boolean;
  /** Server-generated id for this replay batch; ties audits and payment rows together. */
  replayBatchCorrelationId?: string | null;
  data?: ReplayOfflineTransactionsResponseItem[];
};

/** Consecutive replay responses without a complete row for this queue entry; reset when server returns a complete item. */
const MAX_REPLAY_RESPONSE_MISS_RETRIES = 3;

function isReplayResponseItemComplete(
  entryQueueId: string,
  it: ReplayOfflineTransactionsResponseItem | undefined
): boolean {
  if (!it) return false;
  if (String(it.requestedOfflineTransactionId ?? '').trim() !== entryQueueId) return false;
  const oid = it.offlineTransactionId;
  if (oid == null || String(oid).trim() === '') return false;
  return true;
}

export interface PendingPaymentEntry {
  /**
   * Client-side OfflineTransaction id.
   * Must be a UUID so backend can persist it as OfflineTransaction.Id.
   */
  queueId: string;

  createdAt: string;
  cashRegisterId: string;

  /** Device identifier for monotonic client sequence tracking (optional for legacy entries). */
  deviceId?: string | null;

  /** Monotonic increasing client sequence number for (deviceId + cashRegisterId). */
  clientSequenceNumber?: number | null;

  /** Original request payload used for replay. */
  paymentRequest: PendingPaymentPayload;

  /** Non-fiscal queue state. */
  status: OfflineTransactionStatus;

  /** Populated only after replay. */
  syncedPaymentId?: string | null;

  /** Replay batch correlation ID from last replay response; for support/incident correlation. */
  replayBatchCorrelationId?: string | null;

  /**
   * Consecutive times a replay HTTP response omitted this entry or omitted `offlineTransactionId`.
   * Reset when a complete matching response item is received. Used to escalate to Unknown after max retries.
   */
  replayResponseMissStreak?: number;

  /** Legacy flag (kept for backward compatibility with old code). */
  isSynced: boolean;

  lastAttemptAt?: string;
  lastError?: string;
}

async function readQueue(): Promise<PendingPaymentEntry[]> {
  // 1) New format
  const rawNew = await storage.getItem(STORAGE_KEY);
  if (rawNew) {
    try {
      const parsed = JSON.parse(rawNew) as unknown;
      return Array.isArray(parsed) ? (parsed as PendingPaymentEntry[]) : [];
    } catch {
      return [];
    }
  }

  // 2) Legacy format migration
  const rawLegacy = await storage.getItem(LEGACY_STORAGE_KEY);
  if (!rawLegacy) return [];
  try {
    const parsed = JSON.parse(rawLegacy) as unknown;
    if (!Array.isArray(parsed)) return [];

    // Legacy entries are only "unsynced pending" in practice.
    return (parsed as any[]).map((e) => ({
      queueId: String(e.queueId ?? ''),
      createdAt: String(e.createdAt ?? new Date().toISOString()),
      cashRegisterId: String(
        e.paymentRequest?.cashRegisterId ?? e.cashRegisterId ?? ''
      ),
      paymentRequest: e.paymentRequest as PendingPaymentPayload,
      status: 'Pending' as OfflineTransactionStatus,
      syncedPaymentId: null,
      isSynced: false,
      deviceId: undefined,
      clientSequenceNumber: undefined,
      lastAttemptAt: e.lastAttemptAt,
      lastError: e.lastError,
    }));
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

function normalizeEntry(e: PendingPaymentEntry): PendingPaymentEntry {
  return {
    ...e,
    status: e.status ?? (e.isSynced ? 'Synced' : 'Pending'),
    isSynced: e.isSynced ?? (e.status === 'Synced'),
    syncedPaymentId: e.syncedPaymentId ?? null,
    cashRegisterId: e.cashRegisterId ?? e.paymentRequest.cashRegisterId,
    deviceId: e.deviceId ?? null,
    clientSequenceNumber: e.clientSequenceNumber ?? null,
    replayBatchCorrelationId: e.replayBatchCorrelationId ?? null,
    replayResponseMissStreak: e.replayResponseMissStreak ?? 0,
  };
}

async function getOrCreateDeviceId(): Promise<string> {
  const existing = await storage.getItem(DEVICE_ID_KEY);
  if (existing) return existing;
  const id =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
  await storage.setItem(DEVICE_ID_KEY, id);
  return id;
}

async function nextClientSequenceNumber(cashRegisterId: string): Promise<number> {
  const raw = await storage.getItem(SEQUENCE_MAP_KEY);
  let map: Record<string, number> = {};
  if (raw) {
    try {
      map = JSON.parse(raw) as Record<string, number>;
    } catch {
      map = {};
    }
  }
  const current = map[cashRegisterId] ?? 0;
  const next = current + 1;
  map[cashRegisterId] = next;
  await storage.setItem(SEQUENCE_MAP_KEY, JSON.stringify(map));
  return next;
}

/**
 * Append payment to offline queue (or return existing queue id for same idempotency key).
 */
export async function enqueuePendingPayment(
  paymentRequest: PendingPaymentPayload
): Promise<string> {
  if (paymentPayloadContainsVoucherSecrets(paymentRequest.payment)) {
    throw new Error(VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE);
  }

  const qRaw = await readQueue();
  const q = qRaw.map(normalizeEntry);
  const idem = paymentRequest.idempotencyKey?.trim();

  if (idem) {
    const existing = q.find(
      (e) =>
        e.status === 'Pending' && e.paymentRequest.idempotencyKey === idem
    );
    if (existing) return existing.queueId;
  }

  const entry: PendingPaymentEntry = {
    queueId: newQueueId(),
    paymentRequest: { ...paymentRequest },
    createdAt: new Date().toISOString(),
    cashRegisterId: paymentRequest.cashRegisterId,
    status: 'Pending',
    syncedPaymentId: null,
    isSynced: false,
    deviceId: await getOrCreateDeviceId(),
    clientSequenceNumber: await nextClientSequenceNumber(paymentRequest.cashRegisterId),
  };

  q.push(entry);
  await writeQueue(q);
  return entry.queueId;
}

export async function getPendingPaymentQueue(): Promise<PendingPaymentEntry[]> {
  const q = (await readQueue()).map(normalizeEntry);
  return q
    .filter((e) => e.status === 'Pending')
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

/**
 * Returns all queue entries (Pending, Synced, Failed, Unknown) for operator visibility.
 * Sorted by createdAt descending (newest first).
 */
export async function getAllQueueEntries(): Promise<PendingPaymentEntry[]> {
  const q = (await readQueue()).map(normalizeEntry);
  return q.sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  );
}

export async function removePendingByQueueId(queueId: string): Promise<void> {
  const q = (await readQueue()).map(normalizeEntry);
  await writeQueue(q.filter((e) => e.queueId !== queueId));
}

export async function removePendingByIdempotencyKey(key: string): Promise<void> {
  if (!key?.trim()) return;
  const q = (await readQueue()).map(normalizeEntry);
  await writeQueue(
    q.filter((e) => e.paymentRequest.idempotencyKey !== key)
  );
}

async function touchAttempt(queueId: string, err: string, status?: OfflineTransactionStatus): Promise<void> {
  const q = (await readQueue()).map(normalizeEntry);
  const e = q.find((x) => x.queueId === queueId);
  if (e) {
    e.lastAttemptAt = new Date().toISOString();
    e.lastError = err;
    if (status) {
      e.status = status;
      e.isSynced = status === 'Synced';
      if (status !== 'Synced') e.syncedPaymentId = null;
    }
    await writeQueue(q);
  }
}

/**
 * Replay all pending offline transactions against backend in original order.
 * Updates local status; never generates fiscal receipt/signature on offline entries.
 */
export async function syncPendingPaymentQueue(): Promise<{
  processed: number;
  failed: number;
}> {
  const pending = await getPendingPaymentQueue();
  if (pending.length === 0) return { processed: 0, failed: 0 };

  let processed = 0;
  let failed = 0;

  try {
    const req = {
      transactions: pending.map((entry) => ({
        offlineTransactionId: entry.queueId,
        createdAtUtc: entry.createdAt,
        cashRegisterId: entry.cashRegisterId,
        payload: entry.paymentRequest,
        deviceId: entry.deviceId ?? undefined,
        clientSequenceNumber: entry.clientSequenceNumber ?? undefined,
      })),
    };

    const raw = await apiClient.post<ReplayOfflineTransactionsResponse>(
      '/offline-transactions/replay',
      req
    );

    const items =
      (raw as ReplayOfflineTransactionsResponse)?.data ??
      (raw as any)?.Value?.data ??
      [];
    const batchCorrelationId =
      (raw as ReplayOfflineTransactionsResponse)?.replayBatchCorrelationId ??
      (raw as any)?.replayBatchCorrelationId ??
      null;
    const batchIdStr = batchCorrelationId != null ? String(batchCorrelationId) : null;

    const q = (await readQueue()).map(normalizeEntry);
    const byId = new Map<string, ReplayOfflineTransactionsResponseItem>();
    for (const it of items ?? []) {
      if (it?.requestedOfflineTransactionId) {
        byId.set(String(it.requestedOfflineTransactionId), it);
      }
    }

    for (const entry of pending) {
      const rawIt = byId.get(entry.queueId);
      const e = q.find((x) => x.queueId === entry.queueId);
      if (!e) continue;
      e.lastAttemptAt = new Date().toISOString();
      if (batchIdStr) e.replayBatchCorrelationId = batchIdStr;

      if (!isReplayResponseItemComplete(entry.queueId, rawIt)) {
        const prev = e.replayResponseMissStreak ?? 0;
        const streak = prev + 1;
        e.replayResponseMissStreak = streak;
        console.warn(
          '[pendingPaymentQueue] Replay response missing or incomplete row for offline entry; operator should verify server logs.',
          { queueId: entry.queueId, replayBatchCorrelationId: batchIdStr, missStreak: streak }
        );
        if (streak >= MAX_REPLAY_RESPONSE_MISS_RETRIES) {
          e.status = 'Unknown';
          e.isSynced = false;
          e.syncedPaymentId = null;
          e.lastError =
            'Server-Antwort wiederholt unvollständig (offlineTransactionId fehlt). Bitte Support mit Replay-Batch-ID informieren.';
        } else {
          e.status = 'Pending';
          e.isSynced = false;
          e.syncedPaymentId = null;
          e.lastError = `replay_response_incomplete (Versuch ${streak}/${MAX_REPLAY_RESPONSE_MISS_RETRIES}, erneuter Sync empfohlen)`;
        }
        continue;
      }

      const it = rawIt!;
      e.replayResponseMissStreak = 0;

      if (it?.status === 'Synced' || it?.status === 'synced') {
        e.status = 'Synced';
        e.isSynced = true;
        e.syncedPaymentId = it?.syncedPaymentId ?? null;
        e.lastError = undefined;
        processed++;
      } else if (it?.status === 'Pending' || it?.status === 'pending') {
        e.status = 'Pending';
        e.isSynced = false;
        e.syncedPaymentId = null;
        const code = it?.errorCode ? String(it.errorCode) : '';
        const msg =
          it?.lastErrorMessageSafe ?? it?.error ?? 'offline_sync_failed';
        const hint = it?.exponentialBackoffHintSeconds
          ? ` (Retry hint: ${it.exponentialBackoffHintSeconds}s)`
          : '';
        e.lastError = code ? `[${code}] ${msg}${hint}` : `${msg}${hint}`;
      } else {
        e.status = 'Failed';
        e.isSynced = false;
        e.syncedPaymentId = null;
        const code = it?.errorCode ? String(it.errorCode) : '';
        const msg =
          it?.lastErrorMessageSafe ?? it?.error ?? 'offline_sync_failed';
        e.lastError = code ? `[${code}] ${msg}` : msg;
        failed++;
      }
    }

    await writeQueue(q);
  } catch (e) {
    // Transport-level failure: keep entries Pending; just update lastError for operator visibility.
    const msg = e instanceof Error ? e.message : 'sync_transport_failed';
    for (const entry of pending) {
      await touchAttempt(entry.queueId, msg);
    }
    failed = pending.length;
  }

  return { processed, failed };
}

/**
 * Retry sync for a single queue entry. Sends one transaction to replay endpoint.
 * Safe: Pending, Failed, or Unknown entries; backend handles idempotency. Unknown is cleared to Pending when operator retries.
 */
export async function retrySinglePending(queueId: string): Promise<{
  processed: number;
  failed: number;
}> {
  const all = (await readQueue()).map(normalizeEntry);
  const entry = all.find((e) => e.queueId === queueId);
  if (!entry) return { processed: 0, failed: 0 };
  if (entry.status !== 'Pending' && entry.status !== 'Failed' && entry.status !== 'Unknown') {
    return { processed: 0, failed: 0 };
  }

  {
    const qReset = (await readQueue()).map(normalizeEntry);
    const er = qReset.find((x) => x.queueId === queueId);
    if (er) {
      er.replayResponseMissStreak = 0;
      if (er.status === 'Unknown') {
        er.status = 'Pending';
        er.isSynced = false;
      }
      await writeQueue(qReset);
    }
  }

  const req = {
    transactions: [
      {
        offlineTransactionId: entry.queueId,
        createdAtUtc: entry.createdAt,
        cashRegisterId: entry.cashRegisterId,
        payload: entry.paymentRequest,
        deviceId: entry.deviceId ?? undefined,
        clientSequenceNumber: entry.clientSequenceNumber ?? undefined,
      },
    ],
  };
  let processed = 0;
  let failed = 0;
  try {
    const raw = await apiClient.post<ReplayOfflineTransactionsResponse>(
      '/offline-transactions/replay',
      req
    );
    const items =
      (raw as ReplayOfflineTransactionsResponse)?.data ?? (raw as any)?.Value?.data ?? [];
    const rawIt = items?.find(
      (x) => String(x?.requestedOfflineTransactionId ?? '').trim() === queueId
    );
    const batchCorrelationId =
      (raw as ReplayOfflineTransactionsResponse)?.replayBatchCorrelationId ??
      (raw as any)?.replayBatchCorrelationId ??
      null;
    const q = (await readQueue()).map(normalizeEntry);
    const e = q.find((x) => x.queueId === queueId);
    if (!e) return { processed: 0, failed: 0 };
    e.lastAttemptAt = new Date().toISOString();
    if (batchCorrelationId != null) e.replayBatchCorrelationId = String(batchCorrelationId);

    if (!isReplayResponseItemComplete(queueId, rawIt)) {
      const streak = (e.replayResponseMissStreak ?? 0) + 1;
      e.replayResponseMissStreak = streak;
      const batchStr = batchCorrelationId != null ? String(batchCorrelationId) : null;
      console.warn(
        '[pendingPaymentQueue] Replay response missing or incomplete row for offline entry; operator should verify server logs.',
        { queueId, replayBatchCorrelationId: batchStr, missStreak: streak }
      );
      if (streak >= MAX_REPLAY_RESPONSE_MISS_RETRIES) {
        e.status = 'Unknown';
        e.isSynced = false;
        e.syncedPaymentId = null;
        e.lastError =
          'Server-Antwort wiederholt unvollständig (offlineTransactionId fehlt). Bitte Support mit Replay-Batch-ID informieren.';
      } else {
        e.status = 'Pending';
        e.isSynced = false;
        e.syncedPaymentId = null;
        e.lastError = `replay_response_incomplete (Versuch ${streak}/${MAX_REPLAY_RESPONSE_MISS_RETRIES}, erneuter Sync empfohlen)`;
      }
      await writeQueue(q);
      return { processed: 0, failed: 0 };
    }

    const it = rawIt!;
    e.replayResponseMissStreak = 0;

    if (it?.status === 'Synced' || it?.status === 'synced') {
      e.status = 'Synced';
      e.isSynced = true;
      e.syncedPaymentId = it?.syncedPaymentId ?? null;
      e.lastError = undefined;
      processed = 1;
    } else if (it?.status === 'Pending' || it?.status === 'pending') {
      e.status = 'Pending';
      e.isSynced = false;
      e.syncedPaymentId = null;
      const code = it?.errorCode ? String(it.errorCode) : '';
      const msg = it?.lastErrorMessageSafe ?? it?.error ?? 'offline_sync_failed';
      const hint = it?.exponentialBackoffHintSeconds
        ? ` (Retry hint: ${it.exponentialBackoffHintSeconds}s)`
        : '';
      e.lastError = code ? `[${code}] ${msg}${hint}` : `${msg}${hint}`;
    } else {
      e.status = 'Failed';
      e.isSynced = false;
      e.syncedPaymentId = null;
      const code = it?.errorCode ? String(it.errorCode) : '';
      const msg = it?.lastErrorMessageSafe ?? it?.error ?? 'offline_sync_failed';
      e.lastError = code ? `[${code}] ${msg}` : msg;
      failed = 1;
    }
    await writeQueue(q);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'sync_transport_failed';
    await touchAttempt(queueId, msg);
    failed = 1;
  }
  return { processed, failed };
}
