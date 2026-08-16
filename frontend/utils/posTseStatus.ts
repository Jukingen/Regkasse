export type TseOperationalHealthStatus = 'Online' | 'Degraded' | 'Offline';
export type PosTseIndicatorStatus = 'Active' | 'Degraded' | 'Inactive';

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
