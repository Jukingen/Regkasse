/**
 * Menu ↔ permission mapping consistency analysis for FA admin tooling.
 */
import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';
import {
  MENU_PERMISSION_MAP,
  getAllMenuKeys,
  type MenuPermissionMapKey,
} from '@/shared/auth/menuPermissionMapping';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';

import {
  getMenuItemsAffectedByPermission,
  getPermissionsAffectingMenu,
  listSidebarMenuFilterOptions,
} from './permissionMenuImpact';

export type ConsistencySeverity = 'ok' | 'warning' | 'error';

export type ConsistencyIssue = {
  id: string;
  severity: ConsistencySeverity;
  kind:
    | 'menu_without_permission'
    | 'permission_without_menu'
    | 'incorrect_permission_key'
    | 'unwired_menu_area';
  /** Human-readable subject (menu path or permission key). */
  subject: string;
  detail?: string;
  /** Suggested canonical permission key(s), when applicable. */
  suggestedKeys?: string[];
};

export type ConsistencyReport = {
  checkedAt: string;
  okMenus: number;
  issues: ConsistencyIssue[];
  summary: {
    ok: number;
    warning: number;
    error: number;
  };
};

/** Known inventedish aliases → canonical keys. */
export const INVENTED_PERMISSION_ALIASES: Readonly<Record<string, string>> = {
  'users.view': 'user.view',
  'users.manage': 'user.manage',
  'cashregister.view': 'cash_register.view',
  'cashregister.manage': 'cash_register.manage',
  'dailyclosing.view': 'daily-closing.view',
  'dailyclosing.execute': 'daily-closing.execute',
  'dashboard.view': '', // intentional hub — no catalog key
  'backup.view': 'settings.view',
  'rksv.view': 'finanzonline.manage',
  'rksv.manage': 'finanzonline.manage',
  'sale.manage': 'sale.view',
};

const FA_CATALOG_KEYS = new Set<string>(Object.values(PERMISSIONS));

function isAnyAuthGate(required: string | readonly string[] | undefined): boolean {
  if (required === undefined) return false;
  if (Array.isArray(required)) return required.length === 0;
  return false;
}

/**
 * Analyze sidebar catalog / MENU_PERMISSION_MAP / route gates for mapping gaps.
 * @param permissionCatalogKeys optional full backend/FA catalog keys (defaults to typed PERMISSIONS).
 */
export function analyzeMenuPermissionConsistency(
  permissionCatalogKeys: readonly string[] = [...FA_CATALOG_KEYS]
): ConsistencyReport {
  const catalogSet = new Set(permissionCatalogKeys);
  const issues: ConsistencyIssue[] = [];
  let okMenus = 0;

  const menuOptions = listSidebarMenuFilterOptions();
  for (const opt of menuOptions) {
    const reqs = getPermissionsAffectingMenu(opt.value);
    const catalogItem = Object.values(SIDEBAR_NAV_ITEM_CATALOG).find(
      (i) => i.menuKey === opt.value
    );
    const routeGate = ROUTE_PERMISSIONS[opt.value];
    const anyAuth = isAnyAuthGate(catalogItem?.permission) || isAnyAuthGate(routeGate);

    if (anyAuth || reqs.length > 0) {
      okMenus += 1;
      for (const req of reqs) {
        if (INVENTED_PERMISSION_ALIASES[req.key] !== undefined) {
          const suggested = INVENTED_PERMISSION_ALIASES[req.key];
          issues.push({
            id: `incorrect:${opt.value}:${req.key}`,
            severity: 'error',
            kind: 'incorrect_permission_key',
            subject: opt.value,
            detail: req.key,
            suggestedKeys: suggested ? [suggested] : [],
          });
        } else if (!catalogSet.has(req.key) && !req.key.includes('.')) {
          issues.push({
            id: `incorrect:${opt.value}:${req.key}`,
            severity: 'error',
            kind: 'incorrect_permission_key',
            subject: opt.value,
            detail: req.key,
          });
        }
      }
      continue;
    }

    issues.push({
      id: `menu-unmapped:${opt.value}`,
      severity: 'warning',
      kind: 'menu_without_permission',
      subject: opt.value,
      detail: opt.labelKey,
      suggestedKeys: suggestPermissionKeysForMenu(opt.value),
    });
  }

  // MENU_PERMISSION_MAP areas not wired onto catalog
  const wiredAreas = new Set(
    Object.values(SIDEBAR_NAV_ITEM_CATALOG)
      .map((i) => i.menuArea)
      .filter(Boolean) as MenuPermissionMapKey[]
  );
  for (const area of getAllMenuKeys()) {
    if (!wiredAreas.has(area)) {
      issues.push({
        id: `unwired-area:${area}`,
        severity: 'warning',
        kind: 'unwired_menu_area',
        subject: area,
        detail: MENU_PERMISSION_MAP[area].permissionKey,
      });
    }
  }

  // Permissions with no menu impact (view/manage-style only to reduce noise)
  for (const key of permissionCatalogKeys) {
    if (!shouldWarnPermissionWithoutMenu(key)) continue;
    const menus = getMenuItemsAffectedByPermission(key);
    if (menus.length > 0) continue;
    // Skip if another permission in the same resource already covers menus via implication parent
    issues.push({
      id: `perm-unmapped:${key}`,
      severity: 'warning',
      kind: 'permission_without_menu',
      subject: key,
    });
  }

  // Deduplicate by id
  const byId = new Map(issues.map((i) => [i.id, i]));
  const unique = [...byId.values()];

  const summary = {
    ok: okMenus,
    warning: unique.filter((i) => i.severity === 'warning').length,
    error: unique.filter((i) => i.severity === 'error').length,
  };

  return {
    checkedAt: new Date().toISOString(),
    okMenus,
    issues: unique.sort((a, b) => a.severity.localeCompare(b.severity) || a.subject.localeCompare(b.subject)),
    summary,
  };
}

/** Plain-text summary lines for UI / logs (locale-agnostic subjects). */
export function formatConsistencySummaryLines(report: ConsistencyReport): string[] {
  const lines: string[] = [`✅ ${report.okMenus} menus mapped correctly`];
  const unmappedMenus = report.issues
    .filter((i) => i.kind === 'menu_without_permission')
    .map((i) => i.subject);
  if (unmappedMenus.length) {
    lines.push(
      `⚠️ ${unmappedMenus.length} menus unmapped: ${unmappedMenus.map((s) => `'${s}'`).join(', ')}`
    );
  }
  const unmappedPerms = report.issues
    .filter((i) => i.kind === 'permission_without_menu')
    .map((i) => i.subject);
  if (unmappedPerms.length) {
    lines.push(
      `⚠️ ${unmappedPerms.length} permissions without menu: ${unmappedPerms.map((s) => `'${s}'`).join(', ')}`
    );
  }
  const badKeys = report.issues.filter((i) => i.kind === 'incorrect_permission_key');
  if (badKeys.length) {
    lines.push(
      `❌ ${badKeys.length} incorrect permission keys: ${badKeys
        .map((i) => `'${i.subject}'→'${i.detail}'`)
        .join(', ')}`
    );
  }
  return lines;
}

function shouldWarnPermissionWithoutMenu(key: string): boolean {
  // Focus on primary UI gates; skip create/delete/submit noise.
  return (
    key.endsWith('.view') ||
    key.endsWith('.manage') ||
    key.endsWith('.read') ||
    key === 'backup.manage' ||
    key === 'system.critical'
  );
}

/** Suggest permission keys from ROUTE_PERMISSIONS / path heuristics. */
export function suggestPermissionKeysForMenu(menuKey: string): string[] {
  const route = ROUTE_PERMISSIONS[menuKey];
  if (typeof route === 'string' && route) return [route];
  if (Array.isArray(route) && route.length) return [...route];

  // Path heuristics
  if (menuKey.includes('tagesabschluss') || menuKey.includes('daily-closing')) {
    return ['daily-closing.view'];
  }
  if (menuKey.includes('table')) return ['table.view'];
  if (menuKey.includes('product')) return ['product.view'];
  if (menuKey.includes('customer')) return ['customer.view'];
  return [];
}

export type ConsistencyFixSuggestion = {
  issueId: string;
  title: string;
  /** Pseudo-mapping snippet for docs / PR (not auto-applied to source). */
  mappingSnippet: string;
};

/** Build human fix suggestions for common consistency issues. */
export function buildConsistencyFixSuggestions(
  report: ConsistencyReport
): ConsistencyFixSuggestion[] {
  const out: ConsistencyFixSuggestion[] = [];
  for (const issue of report.issues) {
    if (issue.kind === 'incorrect_permission_key' && issue.suggestedKeys?.length) {
      out.push({
        issueId: issue.id,
        title: `Replace ${issue.detail} → ${issue.suggestedKeys.join(', ')}`,
        mappingSnippet: `// ${issue.subject}\npermission: '${issue.suggestedKeys[0]}',`,
      });
      continue;
    }
    if (issue.kind === 'menu_without_permission') {
      const keys = issue.suggestedKeys?.length
        ? issue.suggestedKeys
        : suggestPermissionKeysForMenu(issue.subject);
      if (!keys.length) continue;
      out.push({
        issueId: issue.id,
        title: `Map ${issue.subject} → ${keys.join(' ∨ ')}`,
        mappingSnippet: `// SIDEBAR_NAV_ITEM_CATALOG leaf for ${issue.subject}\npermission: '${keys[0]}',`,
      });
    }
  }
  return out;
}

const DAILY_CHECK_STORAGE_KEY = 'fa_menu_permission_consistency_last_run_v1';

/** Returns true if a daily consistency check should run now. */
export function shouldRunDailyConsistencyCheck(now = Date.now()): boolean {
  if (typeof window === 'undefined') return false;
  try {
    const raw = window.localStorage.getItem(DAILY_CHECK_STORAGE_KEY);
    if (!raw) return true;
    const last = Number(raw);
    if (!Number.isFinite(last)) return true;
    return now - last > 24 * 60 * 60 * 1000;
  } catch {
    return false;
  }
}

export function markDailyConsistencyCheckRun(now = Date.now()): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(DAILY_CHECK_STORAGE_KEY, String(now));
  } catch {
    // ignore
  }
}

/** Dev/ops: run analysis and console.warn when issues exist. */
export function runAndLogMenuPermissionConsistencyCheck(
  permissionCatalogKeys?: readonly string[]
): ConsistencyReport {
  const report = analyzeMenuPermissionConsistency(permissionCatalogKeys);
  if (report.summary.warning > 0 || report.summary.error > 0) {
    // eslint-disable-next-line no-console -- intentional daily/ops audit
    console.warn('[menu-permission-consistency]', {
      okMenus: report.okMenus,
      warning: report.summary.warning,
      error: report.summary.error,
      issues: report.issues.map((i) => `${i.severity}:${i.kind}:${i.subject}`),
    });
  } else {
    // eslint-disable-next-line no-console -- intentional daily/ops audit
    console.info('[menu-permission-consistency] OK', { okMenus: report.okMenus });
  }
  markDailyConsistencyCheckRun();
  return report;
}
