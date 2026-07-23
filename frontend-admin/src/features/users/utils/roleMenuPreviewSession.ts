/**
 * Session-scoped "test as role" preview override for FA sidebar.
 */
const STORAGE_KEY = 'fa_role_menu_preview_v1';

export type RoleMenuPreviewSession = {
  roleName: string;
  permissions: string[];
  startedAt: string;
};

type Listener = () => void;

let session: RoleMenuPreviewSession | null = null;
const listeners = new Set<Listener>();

function emit(): void {
  for (const l of listeners) l();
}

function readStorage(): RoleMenuPreviewSession | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as RoleMenuPreviewSession;
    if (!parsed?.roleName || !Array.isArray(parsed.permissions)) return null;
    return parsed;
  } catch {
    return null;
  }
}

function writeStorage(next: RoleMenuPreviewSession | null): void {
  if (typeof window === 'undefined') return;
  try {
    if (!next) window.sessionStorage.removeItem(STORAGE_KEY);
    else window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // ignore
  }
}

export function getRoleMenuPreviewSession(): RoleMenuPreviewSession | null {
  if (session) return session;
  session = readStorage();
  return session;
}

export function startRoleMenuPreview(roleName: string, permissions: readonly string[]): void {
  session = {
    roleName,
    permissions: [...permissions],
    startedAt: new Date().toISOString(),
  };
  writeStorage(session);
  emit();
}

export function stopRoleMenuPreview(): void {
  session = null;
  writeStorage(null);
  emit();
}

export function subscribeRoleMenuPreview(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
