import { OFFLINE_CONFIG } from '@/constants/offlineConfig';
import { storage } from '@/utils/storage';

export interface SyncHistoryEntry {
  id: string;
  timestamp: Date;
  type: 'order' | 'payment';
  status: 'success' | 'failed' | 'pending';
  message: string;
  orderId?: string;
}

type StoredSyncHistoryEntry = Omit<SyncHistoryEntry, 'timestamp'> & {
  timestamp: string;
};

const STORAGE_KEY = `${OFFLINE_CONFIG.STORAGE_PREFIX}sync_history_v1`;
const MAX_ENTRIES = 200;

function newEntryId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

function deserializeEntry(row: StoredSyncHistoryEntry): SyncHistoryEntry {
  return {
    ...row,
    timestamp: new Date(row.timestamp),
  };
}

/** Track sync history for debugging and admin diagnostics. */
export class OfflineSyncHistory {
  private static instance: OfflineSyncHistory | undefined;

  static getInstance(): OfflineSyncHistory {
    if (!OfflineSyncHistory.instance) {
      OfflineSyncHistory.instance = new OfflineSyncHistory();
    }
    return OfflineSyncHistory.instance;
  }

  async record(
    entry: Omit<SyncHistoryEntry, 'id' | 'timestamp'> & {
      id?: string;
      timestamp?: Date;
    }
  ): Promise<SyncHistoryEntry> {
    const saved: StoredSyncHistoryEntry = {
      id: entry.id ?? newEntryId(),
      timestamp: (entry.timestamp ?? new Date()).toISOString(),
      type: entry.type,
      status: entry.status,
      message: entry.message,
      orderId: entry.orderId,
    };

    const history = await this.readAll();
    history.unshift(saved);
    await this.writeAll(history.slice(0, MAX_ENTRIES));
    return deserializeEntry(saved);
  }

  async recordOrderSync(
    orderId: string,
    status: SyncHistoryEntry['status'],
    message: string
  ): Promise<void> {
    await this.record({
      type: 'order',
      status,
      message,
      orderId: orderId || undefined,
    });
  }

  async recordPaymentSync(
    paymentId: string,
    status: SyncHistoryEntry['status'],
    message: string
  ): Promise<void> {
    await this.record({
      type: 'payment',
      status,
      message,
      orderId: paymentId || undefined,
    });
  }

  async getHistory(limit = 50): Promise<SyncHistoryEntry[]> {
    const history = await this.readAll();
    return history.slice(0, limit).map(deserializeEntry);
  }

  async clear(): Promise<void> {
    await storage.removeItem(STORAGE_KEY);
  }

  private async readAll(): Promise<StoredSyncHistoryEntry[]> {
    try {
      const parsed = await storage.getJson<unknown>(STORAGE_KEY);
      return Array.isArray(parsed) ? (parsed as StoredSyncHistoryEntry[]) : [];
    } catch {
      return [];
    }
  }

  private async writeAll(entries: StoredSyncHistoryEntry[]): Promise<void> {
    await storage.setJson(STORAGE_KEY, entries);
  }

  static resetForTests(): void {
    OfflineSyncHistory.instance = undefined;
  }
}
