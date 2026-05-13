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
  simulateOfflineMode: process.env.EXPO_PUBLIC_SIMULATE_OFFLINE_MODE === 'true',
  simulateNetworkDelay: parseNetworkDelayMs(process.env.EXPO_PUBLIC_SIMULATE_NETWORK_DELAY_MS),
};

let runtimeOfflineSimulation: boolean | null = null;
let runtimeNetworkDelayMs: number | null = null;

export function isSimulateOfflineModeActive(): boolean {
  if (!isDevelopmentSimulationEnvironment()) return false;
  if (runtimeOfflineSimulation !== null) return runtimeOfflineSimulation;
  return DevFlags.simulateOfflineMode;
}

export function getSimulateNetworkDelayMs(): number {
  if (!isDevelopmentSimulationEnvironment()) return 0;
  if (runtimeNetworkDelayMs !== null) return Math.max(0, runtimeNetworkDelayMs);
  return DevFlags.simulateNetworkDelay;
}

/** null = follow .env again */
export function setRuntimeOfflineSimulationOverride(enabled: boolean | null): void {
  if (!isDevelopmentSimulationEnvironment()) return;
  runtimeOfflineSimulation = enabled;
}

/** null = follow .env again */
export function setRuntimeNetworkDelayMsOverride(ms: number | null): void {
  if (!isDevelopmentSimulationEnvironment()) return;
  runtimeNetworkDelayMs = ms;
}

let loggedOfflineSimulation: boolean = false;

export function logDevOfflineSimulationOnce(): void {
  if (!isDevelopmentSimulationEnvironment() || !isSimulateOfflineModeActive()) return;
  if (loggedOfflineSimulation) return;
  loggedOfflineSimulation = true;
  console.warn('⚠️ DEV: Offline simulation active');
}

export function resetDevOfflineSimulationLog(): void {
  loggedOfflineSimulation = false;
}

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
  g.__toggleOfflineSimulation = (enabled: boolean) => {
    setRuntimeOfflineSimulationOverride(enabled);
    resetDevOfflineSimulationLog();
    console.warn(
      `⚠️ DEV: Offline simulation runtime = ${enabled} (axios requests will ${enabled ? 'fail fast' : 'use network'})`
    );
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
