/** Imperative tenant-switch flag for hooks and non-React callers (persist + impersonation). */

type TenantSwitchListener = (active: boolean) => void;

let isTenantSwitching = false;
const listeners = new Set<TenantSwitchListener>();

export function getIsTenantSwitching(): boolean {
    return isTenantSwitching;
}

export function beginTenantSwitch(): void {
    if (isTenantSwitching) {
        return;
    }
    isTenantSwitching = true;
    listeners.forEach((listener) => listener(true));
}

export function endTenantSwitch(): void {
    if (!isTenantSwitching) {
        return;
    }
    isTenantSwitching = false;
    listeners.forEach((listener) => listener(false));
}

export function subscribeTenantSwitch(listener: TenantSwitchListener): () => void {
    listeners.add(listener);
    listener(isTenantSwitching);
    return () => listeners.delete(listener);
}
