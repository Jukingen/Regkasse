/**
 * Derive visual permission diffs from audit old/new payloads for Änderungshistorie UI.
 */
import {
  parseOverridePayload,
  parseRolePermissionsPayload,
} from '@/features/users/utils/permissionAuditRevert';

export type PermissionDiffChangeKind = 'added' | 'removed' | 'changed' | 'lifecycle';

/** Entry-level color for list cards / tags. */
export type PermissionAuditColor = 'green' | 'red' | 'yellow' | 'blue';

/** Semantic permission state labels used in visual diffs. */
export type PermissionStateKind =
  | 'allowed'
  | 'denied'
  | 'individual'
  | 'absent'
  | 'defaults';

export type PermissionDiffLine = {
  permissionKey: string | null;
  change: PermissionDiffChangeKind;
  oldState: PermissionStateKind | null;
  newState: PermissionStateKind | null;
};

export type PermissionAuditDiff = {
  color: PermissionAuditColor;
  lines: PermissionDiffLine[];
  roleName?: string;
};

export type PermissionAuditDiffSource = {
  action?: string | null;
  oldValues?: string | null;
  newValues?: string | null;
  entityName?: string | null;
  description?: string | null;
};

function toSet(perms: string[] | undefined): Set<string> {
  return new Set((perms ?? []).map((p) => p.trim()).filter(Boolean));
}

function overrideState(payload: {
  isGranted?: boolean;
  removed?: boolean;
} | null): PermissionStateKind | null {
  if (!payload) return 'absent';
  if (payload.removed) return 'absent';
  if (typeof payload.isGranted !== 'boolean') return null;
  return payload.isGranted ? 'individual' : 'denied';
}

function rolePermissionState(hasPermission: boolean): PermissionStateKind {
  return hasPermission ? 'allowed' : 'absent';
}

function colorFromLines(lines: PermissionDiffLine[], action: string): PermissionAuditColor {
  if (action === 'ROLE_CREATE' || action === 'ROLE_DELETE') return 'blue';
  if (lines.length === 0) return 'yellow';
  const kinds = new Set(lines.map((l) => l.change));
  if (kinds.size === 1 && kinds.has('added')) return 'green';
  if (kinds.size === 1 && kinds.has('removed')) return 'red';
  if (kinds.size === 1 && kinds.has('lifecycle')) return 'blue';
  return 'yellow';
}

/**
 * Build diff lines + entry color for a permission-related audit entry.
 */
export function buildPermissionAuditDiff(entry: PermissionAuditDiffSource): PermissionAuditDiff {
  const action = (entry.action ?? '').trim();

  if (action === 'ROLE_CREATE') {
    const created = parseRolePermissionsPayload(entry.newValues);
    const roleName = created?.roleName ?? entry.entityName ?? undefined;
    return {
      color: 'blue',
      roleName,
      lines: [
        {
          permissionKey: roleName ?? null,
          change: 'lifecycle',
          oldState: null,
          newState: 'defaults',
        },
      ],
    };
  }

  if (action === 'ROLE_DELETE') {
    const deleted = parseRolePermissionsPayload(entry.oldValues);
    const roleName = deleted?.roleName ?? entry.entityName ?? undefined;
    return {
      color: 'blue',
      roleName,
      lines: [
        {
          permissionKey: roleName ?? null,
          change: 'lifecycle',
          oldState: 'defaults',
          newState: 'absent',
        },
      ],
    };
  }

  if (action === 'ROLE_PERMISSIONS_UPDATE') {
    const oldPayload = parseRolePermissionsPayload(entry.oldValues);
    const newPayload = parseRolePermissionsPayload(entry.newValues);
    const roleName =
      oldPayload?.roleName ?? newPayload?.roleName ?? entry.entityName ?? undefined;
    const oldSet = toSet(oldPayload?.permissions);
    const newSet = toSet(newPayload?.permissions);
    const lines: PermissionDiffLine[] = [];

    for (const key of [...newSet].sort()) {
      if (!oldSet.has(key)) {
        lines.push({
          permissionKey: key,
          change: 'added',
          oldState: rolePermissionState(false),
          newState: rolePermissionState(true),
        });
      }
    }
    for (const key of [...oldSet].sort()) {
      if (!newSet.has(key)) {
        lines.push({
          permissionKey: key,
          change: 'removed',
          oldState: rolePermissionState(true),
          newState: rolePermissionState(false),
        });
      }
    }

    return {
      color: colorFromLines(lines, action),
      roleName,
      lines,
    };
  }

  if (action === 'USER_PERMISSION_OVERRIDES_CHANGED') {
    const oldPayload = parseOverridePayload(entry.oldValues);
    const newPayload = parseOverridePayload(entry.newValues);
    const permissionKey =
      newPayload?.permission ?? oldPayload?.permission ?? null;
    const oldState = overrideState(oldPayload);
    const newState = overrideState(newPayload);

    let change: PermissionDiffChangeKind = 'changed';
    if (oldState === 'absent' && newState && newState !== 'absent') change = 'added';
    else if (newState === 'absent' && oldState && oldState !== 'absent') change = 'removed';

    const lines: PermissionDiffLine[] = [
      {
        permissionKey,
        change,
        oldState,
        newState,
      },
    ];

    return {
      color: colorFromLines(lines, action),
      lines,
    };
  }

  return { color: 'yellow', lines: [] };
}

/** Ant Design Tag color token for entry-level coding. */
export function permissionAuditTagColor(
  color: PermissionAuditColor
): 'success' | 'error' | 'warning' | 'processing' {
  switch (color) {
    case 'green':
      return 'success';
    case 'red':
      return 'error';
    case 'blue':
      return 'processing';
    case 'yellow':
    default:
      return 'warning';
  }
}

/** CSS border color for list cards. */
export function permissionAuditBorderColor(color: PermissionAuditColor): string {
  switch (color) {
    case 'green':
      return '#52c41a';
    case 'red':
      return '#ff4d4f';
    case 'blue':
      return '#1677ff';
    case 'yellow':
    default:
      return '#faad14';
  }
}
