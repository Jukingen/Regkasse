/**
 * POS offline / TSE / NTP development simulations.
 *
 * Permanently disabled: env flags (`EXPO_PUBLIC_SIMULATE_*`) are ignored so POS never shows
 * simulated offline / degraded connectivity. Real device connectivity and backend responses drive UI.
 */

export function isDevSimulatePosOfflineMaster(): boolean {
  return false;
}

export function isDevSimulatePosNetworkOffline(): boolean {
  return false;
}

export function isDevSimulateTseUnavailable(): boolean {
  return false;
}

export function isDevSimulateNtpCriticalUi(): boolean {
  return false;
}
