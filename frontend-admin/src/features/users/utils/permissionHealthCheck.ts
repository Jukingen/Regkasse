/**
 * Heuristic health checks for role/user permission drafts.
 * Suggests closest ROLE_PRESET and flags missing critical / overly broad grants.
 */
import {
  ROLE_PRESETS,
  type RolePreset,
  getPresetKeysInCatalog,
} from '@/features/users/constants/rolePresets';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

export type PermissionHealthSeverity = 'error' | 'warning' | 'info';

export type PermissionHealthIssue = {
  id: string;
  severity: PermissionHealthSeverity;
  /** i18n key under users.permissionOnboarding.* */
  messageKey: string;
  messageParams?: Record<string, string | number>;
  suggestedPresetId?: string;
};

export type PermissionHealthReport = {
  issues: PermissionHealthIssue[];
  grantedCount: number;
  catalogSize: number;
  coverageRatio: number;
  /** Closest preset id by Jaccard similarity, when useful. */
  suggestedPresetId: string | null;
  suggestedPresetScore: number;
};

/** Dangerous platform keys that Mandanten roles should almost never hold. */
const PLATFORM_CRITICAL_KEYS = [PERMISSIONS.SYSTEM_CRITICAL] as const;

/** If any of these are granted, their companions should usually be present too. */
const CRITICAL_COMPANIONS: ReadonlyArray<{
  id: string;
  whenAnyOf: readonly string[];
  requireAllOf: readonly string[];
  messageKey: string;
}> = [
  {
    id: 'pos-fiscal',
    whenAnyOf: [PERMISSIONS.PAYMENT_TAKE, PERMISSIONS.SALE_CREATE],
    requireAllOf: [PERMISSIONS.TSE_SIGN],
    messageKey: 'users.permissionOnboarding.healthMissingTse',
  },
  {
    id: 'user-manage',
    whenAnyOf: [PERMISSIONS.USER_MANAGE],
    requireAllOf: [PERMISSIONS.USER_VIEW],
    messageKey: 'users.permissionOnboarding.healthMissingUserView',
  },
  {
    id: 'role-manage',
    whenAnyOf: [PERMISSIONS.ROLE_MANAGE],
    requireAllOf: [PERMISSIONS.ROLE_VIEW, PERMISSIONS.USER_VIEW],
    messageKey: 'users.permissionOnboarding.healthMissingRoleView',
  },
  {
    id: 'daily-closing',
    whenAnyOf: [PERMISSIONS.DAILY_CLOSING_EXECUTE],
    requireAllOf: [PERMISSIONS.DAILY_CLOSING_VIEW],
    messageKey: 'users.permissionOnboarding.healthMissingDailyClosingView',
  },
  {
    id: 'backup',
    whenAnyOf: [PERMISSIONS.BACKUP_MANAGE],
    requireAllOf: [PERMISSIONS.SETTINGS_VIEW],
    messageKey: 'users.permissionOnboarding.healthMissingSettingsView',
  },
  {
    id: 'cash-register',
    whenAnyOf: [AppPermissions.CashRegisterManage],
    requireAllOf: [AppPermissions.CashRegisterView],
    messageKey: 'users.permissionOnboarding.healthMissingCashRegisterView',
  },
];

const TOO_MANY_RATIO = 0.55;
const TOO_MANY_ABSOLUTE = 90;
const EMPTY_WARN = true;

function toSet(keys: Iterable<string>): Set<string> {
  return keys instanceof Set ? keys : new Set(keys);
}

function jaccard(a: Set<string>, b: Set<string>): number {
  if (a.size === 0 && b.size === 0) return 1;
  let intersection = 0;
  for (const k of a) {
    if (b.has(k)) intersection += 1;
  }
  const union = a.size + b.size - intersection;
  return union === 0 ? 0 : intersection / union;
}

export function findClosestRolePreset(
  granted: Iterable<string>,
  catalogKeys?: Set<string> | string[]
): { preset: RolePreset; score: number } | null {
  const grantedSet = toSet(granted);
  if (grantedSet.size === 0) return null;

  let best: { preset: RolePreset; score: number } | null = null;
  for (const preset of ROLE_PRESETS) {
    const keys = catalogKeys
      ? getPresetKeysInCatalog(preset, catalogKeys)
      : [...preset.permissionKeys];
    const score = jaccard(grantedSet, new Set(keys));
    if (!best || score > best.score) best = { preset, score };
  }
  return best;
}

/**
 * Analyze a permission set for onboarding warnings and preset suggestions.
 */
export function analyzePermissionHealth(options: {
  granted: Iterable<string>;
  catalogSize: number;
  catalogKeys?: Set<string> | string[];
  /** When true, skip platform-critical warnings (SuperAdmin system roles). */
  allowPlatformCritical?: boolean;
}): PermissionHealthReport {
  const grantedSet = toSet(options.granted);
  const catalogSize = Math.max(0, options.catalogSize);
  const coverageRatio = catalogSize > 0 ? grantedSet.size / catalogSize : 0;
  const issues: PermissionHealthIssue[] = [];

  if (EMPTY_WARN && grantedSet.size === 0) {
    issues.push({
      id: 'empty',
      severity: 'warning',
      messageKey: 'users.permissionOnboarding.healthEmpty',
    });
  }

  if (!options.allowPlatformCritical) {
    for (const key of PLATFORM_CRITICAL_KEYS) {
      if (grantedSet.has(key)) {
        issues.push({
          id: `platform-${key}`,
          severity: 'error',
          messageKey: 'users.permissionOnboarding.healthPlatformCritical',
          messageParams: { permission: key },
        });
      }
    }
  }

  for (const rule of CRITICAL_COMPANIONS) {
    const triggered = rule.whenAnyOf.some((k) => grantedSet.has(k));
    if (!triggered) continue;
    const missing = rule.requireAllOf.filter((k) => !grantedSet.has(k));
    if (missing.length === 0) continue;
    issues.push({
      id: rule.id,
      severity: 'warning',
      messageKey: rule.messageKey,
      messageParams: { missing: missing.join(', ') },
    });
  }

  if (
    catalogSize > 0 &&
    (coverageRatio >= TOO_MANY_RATIO || grantedSet.size >= TOO_MANY_ABSOLUTE)
  ) {
    issues.push({
      id: 'too-many',
      severity: 'warning',
      messageKey: 'users.permissionOnboarding.healthTooMany',
      messageParams: {
        count: grantedSet.size,
        percent: Math.round(coverageRatio * 100),
      },
    });
  }

  const closest = findClosestRolePreset(grantedSet, options.catalogKeys);
  const suggestedPresetId =
    closest && closest.score >= 0.35 && closest.score < 0.95 ? closest.preset.id : null;

  if (suggestedPresetId && closest) {
    issues.push({
      id: 'suggest-preset',
      severity: 'info',
      messageKey: 'users.permissionOnboarding.healthSuggestPreset',
      messageParams: {
        preset: closest.preset.label,
        score: Math.round(closest.score * 100),
      },
      suggestedPresetId,
    });
  }

  return {
    issues,
    grantedCount: grantedSet.size,
    catalogSize,
    coverageRatio,
    suggestedPresetId,
    suggestedPresetScore: closest?.score ?? 0,
  };
}
