type Listener = () => void;

const listeners = new Set<Listener>();

export function subscribePosStatusReconnectRefresh(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function notifyPosStatusReconnectRefresh(): void {
  listeners.forEach((listener) => {
    try {
      listener();
    } catch {
      // non-fatal
    }
  });
}
