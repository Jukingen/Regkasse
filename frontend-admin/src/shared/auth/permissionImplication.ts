/**
 * Mirrors backend PermissionImplication for admin UI (role default / effective display).
 * Composite permissions (e.g. user.manage) satisfy granular keys.
 */

const PARENT_TO_CHILDREN: Readonly<Record<string, readonly string[]>> = {
  'user.manage': [
    'user.create',
    'user.edit',
    'user.delete',
    'user.change.role',
    'user.change.username',
    'user.reset.password',
  ],
  'product.manage': ['product.create', 'product.edit', 'product.delete', 'product.update.stock'],
  'tenant.manage': [
    'tenant.view',
    'tenant.create',
    'tenant.edit',
    'tenant.delete',
    'tenant.impersonate',
  ],
  'settings.manage': ['settings.backup'],
  'audit.cleanup': ['audit.delete'],
};

const CHILD_TO_PARENT: Readonly<Record<string, string>> = Object.entries(PARENT_TO_CHILDREN).reduce(
  (acc, [parent, children]) => {
    for (const child of children) {
      if (!(child in acc)) acc[child] = parent;
    }
    return acc;
  },
  {} as Record<string, string>,
);

function toSet(effective: Iterable<string>): Set<string> {
  return effective instanceof Set ? effective : new Set(effective);
}

/** True when `required` is granted directly, via parent composite, or all children present. */
export function permissionImplied(required: string, effective: Iterable<string>): boolean {
  const set = toSet(effective);
  if (set.has(required)) return true;

  const children = PARENT_TO_CHILDREN[required];
  if (children?.length && children.every((c) => set.has(c))) return true;

  const parent = CHILD_TO_PARENT[required];
  if (parent && set.has(parent)) return true;

  return false;
}
