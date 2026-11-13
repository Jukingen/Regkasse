export type TseOperationalHealthStatus = 'Online' | 'Degraded' | 'Offline';
export type PosTseIndicatorStatus = 'Active' | 'Degraded' | 'Inactive';
export type PosTseSignEnvironment = 'TEST' | 'LIVE';

export function toOperationalHealthFromPosTse(
  indicator: string,
  operationalHealth?: string | null
): TseOperationalHealthStatus {
  const health = (operationalHealth || '').trim();
  if (health === 'Online' || health === 'Degraded' || health === 'Offline') {
    return health;
  }
  const s = (indicator || '').trim();
  if (s === 'Inactive') return 'Offline';
  if (s === 'Degraded') return 'Degraded';
  if (s === 'Active') return 'Online';
  return 'Degraded';
}

function normalizeTseSignEnvironment(value?: string | null): PosTseSignEnvironment | null {
  const raw = (value || '').trim().toUpperCase();
  if (raw === 'LIVE' || raw === 'PROD' || raw === 'PRODUCTION') return 'LIVE';
  if (raw === 'TEST' || raw === 'DEV' || raw === 'DEVELOPMENT' || raw === 'STAGING') return 'TEST';
  return null;
}

/**
 * TEST badge on the POS TSE chip: Fiskaly SIGN AT TEST, or local Expo env that is not LIVE/production.
 * `EXPO_PUBLIC_ENVIRONMENT` is the cashier-facing override; `EXPO_PUBLIC_RELEASE_STAGE` is the existing fallback.
 */
export function shouldShowPosTseTestBadge(apiEnvironment?: string | null): boolean {
  const fromApi = normalizeTseSignEnvironment(apiEnvironment);
  if (fromApi === 'LIVE') return false;
  if (fromApi === 'TEST') return true;

  const expo = normalizeTseSignEnvironment(
    typeof process !== 'undefined'
      ? process.env.EXPO_PUBLIC_ENVIRONMENT || process.env.EXPO_PUBLIC_RELEASE_STAGE
      : undefined
  );
  if (expo === 'LIVE') return false;
  if (expo === 'TEST') return true;

  return typeof __DEV__ !== 'undefined' && __DEV__;
}
