/**
 * Diff between two permission sets (e.g. current draft vs another role).
 */
export type PermissionDiffKind =
  /** Present in both sets. */
  | 'same'
  /** Present only in the base (current) set. */
  | 'onlyBase'
  /** Present only in the compare set. */
  | 'onlyCompare';

export type PermissionDiffEntry = {
  permission: string;
  kind: PermissionDiffKind;
};

export type PermissionRoleDiff = {
  /** Keys present in both. */
  same: string[];
  /** Keys only in base (current role / draft). */
  onlyBase: string[];
  /** Keys only in compare role. */
  onlyCompare: string[];
  /** Per-key kind for all union keys. */
  byPermission: Map<string, PermissionDiffKind>;
  /** Total differing keys (onlyBase + onlyCompare). */
  differenceCount: number;
};

function toSet(keys: Iterable<string>): Set<string> {
  return keys instanceof Set ? keys : new Set(keys);
}

/**
 * Compare base permissions (current editor draft) against another role's set.
 */
export function comparePermissionSets(
  base: Iterable<string>,
  compare: Iterable<string>
): PermissionRoleDiff {
  const baseSet = toSet(base);
  const compareSet = toSet(compare);
  const same: string[] = [];
  const onlyBase: string[] = [];
  const onlyCompare: string[] = [];
  const byPermission = new Map<string, PermissionDiffKind>();

  for (const key of baseSet) {
    if (compareSet.has(key)) {
      same.push(key);
      byPermission.set(key, 'same');
    } else {
      onlyBase.push(key);
      byPermission.set(key, 'onlyBase');
    }
  }
  for (const key of compareSet) {
    if (!baseSet.has(key)) {
      onlyCompare.push(key);
      byPermission.set(key, 'onlyCompare');
    }
  }

  same.sort();
  onlyBase.sort();
  onlyCompare.sort();

  return {
    same,
    onlyBase,
    onlyCompare,
    byPermission,
    differenceCount: onlyBase.length + onlyCompare.length,
  };
}

/** Row highlight for visual diff mode. */
export type PermissionDiffHighlight = 'added' | 'removed' | 'same' | undefined;

export function diffKindToHighlight(kind: PermissionDiffKind | undefined): PermissionDiffHighlight {
  if (kind === 'onlyBase') return 'added';
  if (kind === 'onlyCompare') return 'removed';
  if (kind === 'same') return 'same';
  return undefined;
}
