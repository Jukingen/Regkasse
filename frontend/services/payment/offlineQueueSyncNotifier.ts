/**
 * Notifies subscribers when background offline queue sync completes (e.g. after reconnect).
 * Used to show toast/alert: "X Zahlungen synchronisiert" without coupling useApiManager to UI.
 */

type SyncCompleteCallback = (processed: number, failed: number) => void;

let listeners: SyncCompleteCallback[] = [];

export function subscribeOfflineSyncComplete(cb: SyncCompleteCallback): () => void {
  listeners.push(cb);
  return () => {
    listeners = listeners.filter((l) => l !== cb);
  };
}

export function notifyOfflineSyncComplete(processed: number, failed: number): void {
  if (processed === 0 && failed === 0) return;
  listeners.forEach((cb) => {
    try {
      cb(processed, failed);
    } catch (e) {
      console.warn('[OfflineQueueSyncNotifier] Listener error:', e);
    }
  });
}
