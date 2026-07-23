/**
 * Shared open state for sidebar menu permission popover / Permission Explorer.
 */
type Listener = () => void;

export type MenuPermissionInfoTarget = {
  menuKey: string;
  label: string;
};

let popoverTarget: MenuPermissionInfoTarget | null = null;
let explorerOpen = false;
let explorerMenuKey: string | null = null;
const listeners = new Set<Listener>();

function emit(): void {
  for (const listener of listeners) listener();
}

export function subscribeMenuPermissionInfo(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getMenuPermissionPopoverTarget(): MenuPermissionInfoTarget | null {
  return popoverTarget;
}

export function openMenuPermissionPopover(target: MenuPermissionInfoTarget): void {
  popoverTarget = target;
  emit();
}

export function closeMenuPermissionPopover(): void {
  if (!popoverTarget) return;
  popoverTarget = null;
  emit();
}

export function getPermissionExplorerOpen(): boolean {
  return explorerOpen;
}

export function getPermissionExplorerMenuKey(): string | null {
  return explorerMenuKey;
}

export function openPermissionExplorer(menuKey?: string): void {
  explorerOpen = true;
  explorerMenuKey = menuKey ?? null;
  emit();
}

export function closePermissionExplorer(): void {
  if (!explorerOpen && explorerMenuKey == null) return;
  explorerOpen = false;
  explorerMenuKey = null;
  emit();
}

export function setPermissionExplorerMenuKey(menuKey: string | null): void {
  explorerMenuKey = menuKey;
  emit();
}
