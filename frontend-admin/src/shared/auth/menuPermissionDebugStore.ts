/**
 * Dev-only store for the "Debug Menu Permissions" overlay (Ctrl+Shift+P).
 */
type Listener = () => void;

let open = false;
const listeners = new Set<Listener>();

function emit(): void {
  for (const listener of listeners) listener();
}

export function getMenuPermissionDebugOpen(): boolean {
  return open;
}

export function setMenuPermissionDebugOpen(next: boolean): void {
  if (open === next) return;
  open = next;
  emit();
}

export function toggleMenuPermissionDebug(): void {
  open = !open;
  emit();
}

export function subscribeMenuPermissionDebug(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
