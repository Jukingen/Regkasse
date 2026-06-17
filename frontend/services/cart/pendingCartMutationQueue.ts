/**
 * Offline cart mutation outbox — mirrors pendingPaymentQueue pattern.
 * Phase 1: persistence + ordering; replay wired in Phase 2 (CartContext integration).
 */
import { storage } from '../../utils/storage';

const STORAGE_KEY = '@regkasse/pending_cart_mutations_v1';
const DEVICE_ID_KEY = '@regkasse/device_id_v1';
const CART_SEQUENCE_MAP_KEY = '@regkasse/cart_mutation_sequence_map_v1';

export type CartMutationStatus = 'Pending' | 'Synced' | 'Failed' | 'Unknown';

export type CartItemModifierPayload = {
  id: string;
  name: string;
  price: number;
  quantity: number;
  groupId?: string;
};

export type AddOnSelectionPayload = {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity?: number;
};

export type CartMutationPayload =
  | {
      kind: 'add_item';
      productId: string;
      quantity: number;
      options?: {
        modifiers?: CartItemModifierPayload[];
        productName?: string;
        unitPrice?: number;
      };
    }
  | {
      kind: 'add_item_with_addons';
      baseProductId: string;
      baseProductName: string;
      baseUnitPrice: number;
      addOns: AddOnSelectionPayload[];
    }
  | {
      kind: 'update_quantity';
      quantity: number;
      productId?: string;
      itemId?: string;
    }
  | {
      kind: 'remove_item';
      productId?: string;
      itemId?: string;
    }
  | { kind: 'clear_cart' }
  | {
      kind: 'add_modifier';
      cartItemId: string;
      modifier: Omit<CartItemModifierPayload, 'quantity'> & { quantity?: number };
    }
  | {
      kind: 'increment_modifier';
      cartItemId: string;
      modifierId: string;
    }
  | {
      kind: 'decrement_modifier';
      cartItemId: string;
      modifierId: string;
    }
  | {
      kind: 'remove_modifier';
      cartItemId: string;
      modifierId: string;
    };

/** Split/merge require server coordination — online-only (not queued offline). */
export const CART_MUTATION_ONLINE_ONLY_KINDS = ['split_items', 'merge_tables'] as const;

export interface PendingCartMutationEntry {
  /** Client UUID — stable id for replay correlation. */
  mutationId: string;
  tableNumber: number;
  createdAt: string;
  deviceId: string;
  /** Monotonic per (deviceId + tableNumber). */
  clientSequenceNumber: number;
  payload: CartMutationPayload;
  status: CartMutationStatus;
  idempotencyKey?: string;
  lastAttemptAt?: string;
  lastError?: string;
}

function newMutationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

async function readQueue(): Promise<PendingCartMutationEntry[]> {
  const raw = await storage.getItem(STORAGE_KEY);
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? (parsed as PendingCartMutationEntry[]) : [];
  } catch {
    return [];
  }
}

async function writeQueue(entries: PendingCartMutationEntry[]): Promise<void> {
  await storage.setItem(STORAGE_KEY, JSON.stringify(entries));
}

async function getOrCreateDeviceId(): Promise<string> {
  const existing = await storage.getItem(DEVICE_ID_KEY);
  if (existing) return existing;
  const id = newMutationId();
  await storage.setItem(DEVICE_ID_KEY, id);
  return id;
}

function sequenceKey(tableNumber: number, deviceId: string): string {
  return `${deviceId}:${tableNumber}`;
}

async function nextClientSequenceNumber(
  tableNumber: number,
  deviceId: string
): Promise<number> {
  const raw = await storage.getItem(CART_SEQUENCE_MAP_KEY);
  let map: Record<string, number> = {};
  if (raw) {
    try {
      map = JSON.parse(raw) as Record<string, number>;
    } catch {
      map = {};
    }
  }
  const key = sequenceKey(tableNumber, deviceId);
  const current = map[key] ?? 0;
  const next = current + 1;
  map[key] = next;
  await storage.setItem(CART_SEQUENCE_MAP_KEY, JSON.stringify(map));
  return next;
}

function normalizeEntry(e: PendingCartMutationEntry): PendingCartMutationEntry {
  return {
    ...e,
    status: e.status ?? 'Pending',
  };
}

/**
 * Append cart mutation to offline outbox (or return existing id for same idempotency key).
 */
export async function enqueueCartMutation(
  tableNumber: number,
  payload: CartMutationPayload,
  idempotencyKey?: string
): Promise<string> {
  const qRaw = await readQueue();
  const q = qRaw.map(normalizeEntry);
  const idem = idempotencyKey?.trim();

  if (idem) {
    const existing = q.find(
      (e) => e.status === 'Pending' && e.idempotencyKey === idem
    );
    if (existing) return existing.mutationId;
  }

  const deviceId = await getOrCreateDeviceId();
  const entry: PendingCartMutationEntry = {
    mutationId: newMutationId(),
    tableNumber,
    createdAt: new Date().toISOString(),
    deviceId,
    clientSequenceNumber: await nextClientSequenceNumber(tableNumber, deviceId),
    payload,
    status: 'Pending',
    idempotencyKey: idem,
  };

  q.push(entry);
  await writeQueue(q);
  return entry.mutationId;
}

export async function getPendingCartMutations(): Promise<PendingCartMutationEntry[]> {
  const q = (await readQueue()).map(normalizeEntry);
  return q.filter((e) => e.status === 'Pending');
}

export async function getAllCartMutationEntries(): Promise<PendingCartMutationEntry[]> {
  return (await readQueue()).map(normalizeEntry);
}

export async function countPendingCartMutations(): Promise<number> {
  return (await getPendingCartMutations()).length;
}

export async function removeCartMutationById(mutationId: string): Promise<void> {
  const q = await readQueue();
  await writeQueue(q.filter((e) => e.mutationId !== mutationId));
}

export async function markCartMutationSynced(mutationId: string): Promise<void> {
  const q = (await readQueue()).map(normalizeEntry);
  const idx = q.findIndex((e) => e.mutationId === mutationId);
  if (idx < 0) return;
  q[idx] = { ...q[idx], status: 'Synced', lastError: undefined };
  await writeQueue(q);
}

export async function markCartMutationFailed(
  mutationId: string,
  errorMessage: string
): Promise<void> {
  const q = (await readQueue()).map(normalizeEntry);
  const idx = q.findIndex((e) => e.mutationId === mutationId);
  if (idx < 0) return;
  q[idx] = {
    ...q[idx],
    status: 'Failed',
    lastAttemptAt: new Date().toISOString(),
    lastError: errorMessage,
  };
  await writeQueue(q);
}

export type CartMutationSyncResult = { processed: number; failed: number };

/**
 * Replay pending cart mutations in FIFO order (tableNumber, then clientSequenceNumber).
 * Phase 2: implement per-kind replay via cartService / CartContext APIs.
 * Phase 1: no-op — does not mutate queue entries.
 */
export async function syncPendingCartMutations(): Promise<CartMutationSyncResult> {
  return { processed: 0, failed: 0 };
}

/** Test / dev helper — clears outbox. */
export async function clearCartMutationQueueForTests(): Promise<void> {
  await storage.removeItem(STORAGE_KEY);
  await storage.removeItem(CART_SEQUENCE_MAP_KEY);
}
