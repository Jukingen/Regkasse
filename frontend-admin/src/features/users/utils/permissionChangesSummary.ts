/**
 * Before/after permission diff + aggregated menu impact for onboarding "What's changed?" panel.
 */
import {
  getMenuItemsAffectedByPermission,
  type PermissionMenuImpactItem,
} from '@/features/users/utils/permissionMenuImpact';
import { comparePermissionSets } from '@/features/users/utils/permissionRoleDiff';

export type PermissionChangesSummary = {
  added: string[];
  removed: string[];
  unchangedCount: number;
  /** Menus newly unlockable by added permissions (not already covered by before-set). */
  menusGained: PermissionMenuImpactItem[];
  /** Menus that may disappear when removed permissions are the only unlock. */
  menusLost: PermissionMenuImpactItem[];
};

function menusCoveredBy(permissions: Iterable<string>): Map<string, PermissionMenuImpactItem> {
  const map = new Map<string, PermissionMenuImpactItem>();
  for (const key of permissions) {
    for (const item of getMenuItemsAffectedByPermission(key)) {
      map.set(item.path, item);
    }
  }
  return map;
}

/**
 * Summarize draft vs saved permission sets and resulting menu visibility deltas.
 */
export function summarizePermissionChanges(
  before: Iterable<string>,
  after: Iterable<string>
): PermissionChangesSummary {
  const diff = comparePermissionSets(after, before);
  // onlyBase = in after only = added; onlyCompare = in before only = removed
  const added = diff.onlyBase;
  const removed = diff.onlyCompare;

  const beforeMenus = menusCoveredBy(before);
  const afterMenus = menusCoveredBy(after);

  const menusGained: PermissionMenuImpactItem[] = [];
  for (const [path, item] of afterMenus) {
    if (!beforeMenus.has(path)) menusGained.push(item);
  }
  const menusLost: PermissionMenuImpactItem[] = [];
  for (const [path, item] of beforeMenus) {
    if (!afterMenus.has(path)) menusLost.push(item);
  }

  menusGained.sort((a, b) => a.path.localeCompare(b.path));
  menusLost.sort((a, b) => a.path.localeCompare(b.path));

  return {
    added,
    removed,
    unchangedCount: diff.same.length,
    menusGained,
    menusLost,
  };
}
