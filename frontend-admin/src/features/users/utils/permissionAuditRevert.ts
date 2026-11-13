/**
 * Parse permission-related audit payloads and build revert payloads.
 */
export type PermissionAuditScope = 'role' | 'user';

export const PERMISSION_AUDIT_ACTIONS = [
  'ROLE_CREATE',
  'ROLE_DELETE',
  'ROLE_PERMISSIONS_UPDATE',
  'USER_PERMISSION_OVERRIDES_CHANGED',
] as const;

export type PermissionAuditAction = (typeof PERMISSION_AUDIT_ACTIONS)[number];

export type ParsedRolePermissionsPayload = {
  roleName?: string;
  permissions?: string[];
};

export type ParsedOverridePayload = {
  permission?: string;
  isGranted?: boolean;
  tenantId?: string | null;
  expiresAt?: string | null;
  id?: string;
  removed?: boolean;
  overrideId?: string;
};

function safeParseJson(raw: string | null | undefined): unknown {
  if (!raw?.trim()) return null;
  try {
    return JSON.parse(raw) as unknown;
  } catch {
    return null;
  }
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function asStringArray(value: unknown): string[] | undefined {
  if (!Array.isArray(value)) return undefined;
  return value.filter((v): v is string => typeof v === 'string');
}

export function parseRolePermissionsPayload(
  raw: string | null | undefined
): ParsedRolePermissionsPayload | null {
  const obj = asRecord(safeParseJson(raw));
  if (!obj) return null;
  return {
    roleName: typeof obj.roleName === 'string' ? obj.roleName : undefined,
    permissions: asStringArray(obj.permissions),
  };
}

export function parseOverridePayload(
  raw: string | null | undefined
): ParsedOverridePayload | null {
  const obj = asRecord(safeParseJson(raw));
  if (!obj) return null;
  return {
    permission: typeof obj.permission === 'string' ? obj.permission : undefined,
    isGranted: typeof obj.isGranted === 'boolean' ? obj.isGranted : undefined,
    tenantId:
      typeof obj.tenantId === 'string'
        ? obj.tenantId
        : obj.tenantId === null
          ? null
          : undefined,
    expiresAt:
      typeof obj.expiresAt === 'string'
        ? obj.expiresAt
        : obj.expiresAt === null
          ? null
          : undefined,
    id: typeof obj.id === 'string' ? obj.id : undefined,
    removed: obj.removed === true,
    overrideId:
      typeof obj.overrideId === 'string'
        ? obj.overrideId
        : typeof obj.OverrideId === 'string'
          ? obj.OverrideId
          : undefined,
  };
}

export type RevertCapability =
  | { kind: 'rolePermissions'; roleName: string; permissions: string[] }
  | {
      kind: 'overrideUpsert';
      userId: string;
      permission: string;
      isGranted: boolean;
      tenantId?: string | null;
      expiresAt?: string | null;
    }
  | { kind: 'overrideDelete'; userId: string; overrideId: string }
  | { kind: 'unsupported'; reason: string };

export type AuditRevertSource = {
  action?: string | null;
  oldValues?: string | null;
  newValues?: string | null;
  entityName?: string | null;
  description?: string | null;
};

/**
 * Decide how to revert a permission audit entry (client-driven; no dedicated revert API).
 */
export function resolvePermissionAuditRevert(
  entry: AuditRevertSource,
  context: { roleName?: string | null; userId?: string | null }
): RevertCapability {
  const action = (entry.action ?? '').trim();

  if (action === 'ROLE_PERMISSIONS_UPDATE') {
    const oldPayload = parseRolePermissionsPayload(entry.oldValues);
    const roleName =
      oldPayload?.roleName ??
      context.roleName ??
      entry.entityName ??
      undefined;
    if (!roleName || !oldPayload?.permissions) {
      return { kind: 'unsupported', reason: 'missingOldPermissions' };
    }
    return {
      kind: 'rolePermissions',
      roleName,
      permissions: oldPayload.permissions,
    };
  }

  if (action === 'USER_PERMISSION_OVERRIDES_CHANGED') {
    const userId = context.userId ?? entry.entityName ?? undefined;
    if (!userId) return { kind: 'unsupported', reason: 'missingUserId' };

    const oldPayload = parseOverridePayload(entry.oldValues);
    const newPayload = parseOverridePayload(entry.newValues);

    // Deletion audited → restore previous override via upsert
    if (newPayload?.removed || newPayload?.overrideId) {
      if (!oldPayload?.permission || typeof oldPayload.isGranted !== 'boolean') {
        return { kind: 'unsupported', reason: 'missingOldOverride' };
      }
      return {
        kind: 'overrideUpsert',
        userId,
        permission: oldPayload.permission,
        isGranted: oldPayload.isGranted,
        tenantId: oldPayload.tenantId,
        expiresAt: oldPayload.expiresAt,
      };
    }

    // Upsert audited → if previous existed, restore it; else delete current override
    if (oldPayload?.permission && typeof oldPayload.isGranted === 'boolean') {
      return {
        kind: 'overrideUpsert',
        userId,
        permission: oldPayload.permission,
        isGranted: oldPayload.isGranted,
        tenantId: oldPayload.tenantId,
        expiresAt: oldPayload.expiresAt,
      };
    }

    if (newPayload?.id && newPayload.permission) {
      return {
        kind: 'overrideDelete',
        userId,
        overrideId: newPayload.id,
      };
    }

    return { kind: 'unsupported', reason: 'missingOverrideSnapshot' };
  }

  if (action === 'ROLE_CREATE' || action === 'ROLE_DELETE') {
    return { kind: 'unsupported', reason: 'roleLifecycleManual' };
  }

  return { kind: 'unsupported', reason: 'unknownAction' };
}

export function isPermissionAuditAction(action: string | null | undefined): boolean {
  const a = (action ?? '').trim();
  return (PERMISSION_AUDIT_ACTIONS as readonly string[]).includes(a);
}
