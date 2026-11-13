import NetInfo, { type NetInfoState } from '@react-native-community/netinfo';

import { isDevSimulatePosNetworkOffline } from '@/constants/devSimulatePosOffline';

export type NetworkOnlineState = Pick<NetInfoState, 'isConnected' | 'isInternetReachable'>;

/**
 * Canonical POS online check from NetInfo state.
 *
 * `isInternetReachable === null` is common right after reconnect / on some platforms —
 * treat as online when the link is connected unless reachability is explicitly false.
 */
export function isNetworkOnline(state: NetworkOnlineState): boolean {
  if (isDevSimulatePosNetworkOffline()) {
    return false;
  }
  if (state.isConnected !== true) {
    return false;
  }
  return state.isInternetReachable !== false;
}

function isBrowserOnline(): boolean {
  if (typeof navigator === 'undefined' || !('onLine' in navigator)) {
    return true;
  }
  return navigator.onLine;
}

/** Fetch current NetInfo (or browser) online status using {@link isNetworkOnline}. */
export async function fetchIsNetworkOnline(): Promise<boolean> {
  if (isDevSimulatePosNetworkOffline()) {
    return false;
  }
  try {
    const state = await NetInfo.fetch();
    return isNetworkOnline(state);
  } catch {
    return isBrowserOnline();
  }
}
