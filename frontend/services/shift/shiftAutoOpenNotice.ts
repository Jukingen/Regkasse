/**
 * Last failed POST /api/pos/shift/auto-open, so the cash-register screen can
 * show a message and a retry without replacing the table list with a loader.
 */
type Listener = () => void;

let failureCode: string | null = null;
const listeners = new Set<Listener>();

function emit(): void {
  listeners.forEach((listener) => listener());
}

export function publishShiftAutoOpenFailure(code: string): void {
  failureCode = code;
  emit();
}

export function clearShiftAutoOpenFailure(): void {
  if (failureCode == null) return;
  failureCode = null;
  emit();
}

export function getShiftAutoOpenFailureCode(): string | null {
  return failureCode;
}

export function subscribeShiftAutoOpenFailure(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
