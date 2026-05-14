/**
 * Development-only flags (Expo public env + optional runtime overrides).
 * Blocked unless both React dev mode and non-production NODE_ENV (when process.env exists).
 */

/** True only for local dev bundles — never in production minified builds. */
export function isDevelopmentSimulationEnvironment(): boolean {
  if (typeof __DEV__ !== 'undefined' && !__DEV__) {
    return false;
  }
  if (typeof process !== 'undefined' && process.env?.NODE_ENV === 'production') {
    return false;
  }
  return true;
}

function parseNetworkDelayMs(raw: string | undefined): number {
  const n = parseInt(String(raw ?? '0'), 10);
  if (!Number.isFinite(n) || n < 0) return 0;
  return n;
}

export const DevFlags = {
  /** Offline axios simulation — permanently off (see `isSimulateOfflineModeActive`). */
  simulateOfflineMode: false,
  simulateNetworkDelay: parseNetworkDelayMs(process.env.EXPO_PUBLIC_SIMULATE_NETWORK_DELAY_MS),
};

let runtimeNetworkDelayMs: number | null = null;

/** Always false: POS offline simulation caused incorrect operator-facing status; env is ignored. */
export function isSimulateOfflineModeActive(): boolean {
  return false;
}

export function getSimulateNetworkDelayMs(): number {
  if (!isDevelopmentSimulationEnvironment()) return 0;
  if (runtimeNetworkDelayMs !== null) return Math.max(0, runtimeNetworkDelayMs);
  return DevFlags.simulateNetworkDelay;
}

/** No-op: offline simulation is disabled. */
export function setRuntimeOfflineSimulationOverride(_enabled: boolean | null): void {}

/** null = follow .env again */
export function setRuntimeNetworkDelayMsOverride(ms: number | null): void {
  if (!isDevelopmentSimulationEnvironment()) return;
  runtimeNetworkDelayMs = ms;
}

/** No-op: offline simulation is disabled. */
export function logDevOfflineSimulationOnce(): void {}

/** No-op: offline simulation is disabled. */
export function resetDevOfflineSimulationLog(): void {}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export async function applyDevNetworkDelayIfConfigured(): Promise<void> {
  const ms = getSimulateNetworkDelayMs();
  if (ms <= 0) return;
  await sleep(ms);
}

if (isDevelopmentSimulationEnvironment() && typeof globalThis !== 'undefined') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const g = globalThis as any;
  g.__toggleOfflineSimulation = () => {
    console.warn('⚠️ DEV: Offline simulation is disabled in this app build (ignored).');
  };
  g.__setDevNetworkDelayMs = (ms: number | null) => {
    setRuntimeNetworkDelayMsOverride(ms);
    console.warn(`⚠️ DEV: Network delay override (ms) = ${ms === null ? 'use .env' : ms}`);
  };
  if (typeof window !== 'undefined') {
    window.__toggleOfflineSimulation = g.__toggleOfflineSimulation;
    window.__setDevNetworkDelayMs = g.__setDevNetworkDelayMs;
  }
}

declare global {
  interface Window {
    __toggleOfflineSimulation?: (enabled: boolean) => void;
    __setDevNetworkDelayMs?: (ms: number | null) => void;
  }
}
