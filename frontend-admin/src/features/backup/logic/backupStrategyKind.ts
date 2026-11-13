import type { BackupStrategyKind } from '@/api/generated/model';

/** BackupStrategyKind.Tenant */
const TENANT = 0;
/** BackupStrategyKind.System */
const SYSTEM = 1;

export type BackupStrategyValue = BackupStrategyKind | null | undefined;

/**
 * The generated client types the enum as a number, but some deployments serialize
 * it by name, so both forms are accepted.
 */
export function isSystemBackupStrategy(strategy: BackupStrategyValue): boolean {
  return strategy === SYSTEM || String(strategy) === 'System';
}

export function isTenantBackupStrategy(strategy: BackupStrategyValue): boolean {
  return strategy === TENANT || String(strategy) === 'Tenant';
}
