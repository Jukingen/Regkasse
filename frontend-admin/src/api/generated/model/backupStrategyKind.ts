/**
 * Mirrors backend BackupStrategyKind (numeric enum).
 * Tenant = 0, System = 1.
 */
export type BackupStrategyKind = 0 | 1 | "Tenant" | "System";

export const BackupStrategyKind = {
  Tenant: 0 as const,
  System: 1 as const,
};
