/**
 * Development-only POS offline / TSE / NTP simulation for QA.
 * Never active when __DEV__ is false (production / release builds).
 *
 * Axios "offline" simulation: see `src/config/devFlags.ts` (`isSimulateOfflineModeActive`) and
 * `EXPO_PUBLIC_SIMULATE_OFFLINE_MODE` in `frontend/.env` (Metro restart after changes).
 */

import { isDevelopmentSimulationEnvironment, isSimulateOfflineModeActive } from '../src/config/devFlags';

function envFlagTrue(value: string | undefined): boolean {
  const v = value?.trim().toLowerCase();
  return v === 'true' || v === '1' || v === 'yes';
}

/** Master switch: network offline + TSE unavailable + NTP critical UI (when enabled provider runs). */
export function isDevSimulatePosOfflineMaster(): boolean {
  if (!isDevelopmentSimulationEnvironment()) return false;
  return envFlagTrue(process.env.EXPO_PUBLIC_SIMULATE_POS_OFFLINE);
}

export function isDevSimulatePosNetworkOffline(): boolean {
  if (!isDevelopmentSimulationEnvironment()) return false;
  if (isSimulateOfflineModeActive()) return true;
  if (isDevSimulatePosOfflineMaster()) return true;
  return envFlagTrue(process.env.EXPO_PUBLIC_SIMULATE_POS_NETWORK_OFFLINE);
}

export function isDevSimulateTseUnavailable(): boolean {
  if (!isDevelopmentSimulationEnvironment()) return false;
  if (isDevSimulatePosOfflineMaster()) return true;
  return envFlagTrue(process.env.EXPO_PUBLIC_SIMULATE_TSE_UNAVAILABLE);
}

/** Forces time-sync UI into critical band (does not change backend NTP unless server DevelopmentOptions is used). */
export function isDevSimulateNtpCriticalUi(): boolean {
  if (!isDevelopmentSimulationEnvironment()) return false;
  if (isDevSimulatePosOfflineMaster()) return true;
  return envFlagTrue(process.env.EXPO_PUBLIC_SIMULATE_NTP_CRITICAL);
}
