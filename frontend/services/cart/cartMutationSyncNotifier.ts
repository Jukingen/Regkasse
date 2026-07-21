/**
 * Notifies subscribers when background cart mutation sync completes (e.g. after reconnect).
 */

type CartSyncCompleteCallback = (processed: number, failed: number) => void;

let listeners: CartSyncCompleteCallback[] = [];

export function subscribeCartMutationSyncComplete(cb: CartSyncCompleteCallback): () => void {
  listeners.push(cb);
  return () => {
    listeners = listeners.filter((l) => l !== cb);
  };
}

export function notifyCartMutationSyncComplete(processed: number, failed: number): void {
  if (processed === 0 && failed === 0) return;
  listeners.forEach((cb) => {
    try {
      cb(processed, failed);
    } catch (e) {
      console.warn('[CartMutationSyncNotifier] Listener error:', e);
    }
  });
}
