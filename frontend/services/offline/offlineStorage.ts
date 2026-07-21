import { Platform } from 'react-native';

import { storage } from '../../utils/storage';

const ASYNC_STORAGE_KEY = '@regkasse/offline_orders_storage_v1';
const IDB_NAME = 'regkasse_offline_orders';
const IDB_VERSION = 1;
const IDB_STORE = 'orders';

export type OfflineOrderStatus = 'pending' | 'synced' | 'failed';

export interface OfflineOrder {
  id: string;
  offlineOrderId: string;
  orderData: unknown;
  orderTotal: number;
  paymentMethod: string;
  createdAt: string;
  expiresAt: string;
  status: OfflineOrderStatus;
  /** Backend row id after POST /pos/offline-orders. */
  serverOrderGuid?: string | null;
  syncAttempts?: number;
  lastError?: string | null;
}

export interface IOfflineStorage {
  saveOrder(order: OfflineOrder): Promise<void>;
  getPendingOrders(): Promise<OfflineOrder[]>;
  getOrder(id: string): Promise<OfflineOrder | null>;
  deleteOrder(id: string): Promise<void>;
  updateOrderStatus(id: string, status: string): Promise<void>;
  deleteAllSynced(): Promise<void>;
}

function isOfflineOrderStatus(value: string): value is OfflineOrderStatus {
  return value === 'pending' || value === 'synced' || value === 'failed';
}

function sortByCreatedAt(orders: OfflineOrder[]): OfflineOrder[] {
  return [...orders].sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  );
}

async function readAllFromAsyncStorage(): Promise<OfflineOrder[]> {
  const parsed = await storage.getJson<unknown>(ASYNC_STORAGE_KEY);
  return Array.isArray(parsed) ? (parsed as OfflineOrder[]) : [];
}

async function writeAllToAsyncStorage(orders: OfflineOrder[]): Promise<void> {
  await storage.setJson(ASYNC_STORAGE_KEY, orders);
}

/** Mobile / native: persist offline orders in AsyncStorage (single JSON document). */
export class AsyncStorageAdapter implements IOfflineStorage {
  async saveOrder(order: OfflineOrder): Promise<void> {
    const orders = await readAllFromAsyncStorage();
    const index = orders.findIndex((o) => o.id === order.id);
    if (index >= 0) orders[index] = order;
    else orders.push(order);
    await writeAllToAsyncStorage(orders);
  }

  async getPendingOrders(): Promise<OfflineOrder[]> {
    const orders = await readAllFromAsyncStorage();
    return sortByCreatedAt(orders.filter((o) => o.status === 'pending'));
  }

  async getOrder(id: string): Promise<OfflineOrder | null> {
    const orders = await readAllFromAsyncStorage();
    return orders.find((o) => o.id === id) ?? null;
  }

  async deleteOrder(id: string): Promise<void> {
    const orders = await readAllFromAsyncStorage();
    await writeAllToAsyncStorage(orders.filter((o) => o.id !== id));
  }

  async updateOrderStatus(id: string, status: string): Promise<void> {
    const orders = await readAllFromAsyncStorage();
    const order = orders.find((o) => o.id === id);
    if (!order) return;
    order.status = isOfflineOrderStatus(status) ? status : order.status;
    await writeAllToAsyncStorage(orders);
  }

  async deleteAllSynced(): Promise<void> {
    const orders = await readAllFromAsyncStorage();
    await writeAllToAsyncStorage(orders.filter((o) => o.status !== 'synced'));
  }
}

function openIndexedDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof indexedDB === 'undefined') {
      reject(new Error('IndexedDB is not available in this environment.'));
      return;
    }

    const request = indexedDB.open(IDB_NAME, IDB_VERSION);

    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(IDB_STORE)) {
        const store = db.createObjectStore(IDB_STORE, { keyPath: 'id' });
        store.createIndex('status', 'status', { unique: false });
        store.createIndex('createdAt', 'createdAt', { unique: false });
      }
    };

    request.onsuccess = () => {
      resolve(request.result);
    };
    request.onerror = () => {
      reject(request.error ?? new Error('IndexedDB open failed.'));
    };
  });
}

function idbRequest<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => {
      resolve(request.result);
    };
    request.onerror = () => {
      reject(request.error ?? new Error('IndexedDB request failed.'));
    };
  });
}

function idbTransactionComplete(transaction: IDBTransaction): Promise<void> {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => {
      resolve();
    };
    transaction.onerror = () => {
      reject(transaction.error ?? new Error('IndexedDB transaction failed.'));
    };
    transaction.onabort = () => {
      reject(transaction.error ?? new Error('IndexedDB transaction aborted.'));
    };
  });
}

/** Web POS: persist offline orders in IndexedDB (one row per order). */
export class IndexedDBStorageAdapter implements IOfflineStorage {
  private async withStore<T>(
    mode: IDBTransactionMode,
    fn: (store: IDBObjectStore) => Promise<T>
  ): Promise<T> {
    const db = await openIndexedDb();
    const transaction = db.transaction(IDB_STORE, mode);
    const store = transaction.objectStore(IDB_STORE);
    try {
      const result = await fn(store);
      await idbTransactionComplete(transaction);
      return result;
    } finally {
      db.close();
    }
  }

  async saveOrder(order: OfflineOrder): Promise<void> {
    await this.withStore('readwrite', async (store) => {
      await idbRequest(store.put(order));
    });
  }

  async getPendingOrders(): Promise<OfflineOrder[]> {
    return await this.withStore('readonly', async (store) => {
      const index = store.index('status');
      const rows = await idbRequest(index.getAll('pending'));
      return sortByCreatedAt(rows as OfflineOrder[]);
    });
  }

  async getOrder(id: string): Promise<OfflineOrder | null> {
    return await this.withStore('readonly', async (store) => {
      const row = await idbRequest(store.get(id));
      return (row as OfflineOrder | undefined) ?? null;
    });
  }

  async deleteOrder(id: string): Promise<void> {
    await this.withStore('readwrite', async (store) => {
      await idbRequest(store.delete(id));
    });
  }

  async updateOrderStatus(id: string, status: string): Promise<void> {
    if (!isOfflineOrderStatus(status)) return;

    await this.withStore('readwrite', async (store) => {
      const existing = (await idbRequest(store.get(id))) as OfflineOrder | undefined;
      if (!existing) return;
      await idbRequest(store.put({ ...existing, status }));
    });
  }

  async deleteAllSynced(): Promise<void> {
    await this.withStore('readwrite', async (store) => {
      const index = store.index('status');
      const synced = (await idbRequest(index.getAll('synced'))) as OfflineOrder[];
      await Promise.all(synced.map((order) => idbRequest(store.delete(order.id))));
    });
  }
}

/** Platform-aware offline order storage (IndexedDB on web, AsyncStorage on native). */
export function createOfflineStorage(): IOfflineStorage {
  return Platform.OS === 'web' ? new IndexedDBStorageAdapter() : new AsyncStorageAdapter();
}

/** Shared singleton for queue managers and sync workers. */
let sharedOfflineStorage: IOfflineStorage | null = null;

export function getOfflineStorage(): IOfflineStorage {
  if (!sharedOfflineStorage) {
    sharedOfflineStorage = createOfflineStorage();
  }
  return sharedOfflineStorage;
}
