/**
 * Mirrors backend `PermissionImplication` for admin UI (route/menu/effective display).
 * Keep in sync with `backend/Authorization/PermissionImplication.cs`.
 *
 * Public implication map + helpers: `./permissionImplications.ts`
 */

export const PARENT_TO_CHILDREN: Readonly<Record<string, readonly string[]>> = {
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
  'settings.manage': ['settings.backup', 'backup.manage', 'website.manage'],
  'digital.manage': [
    'digital.view',
    'digital.preview',
    'digital.request',
    'digital.create',
    'digital.publish',
    'digital.edit',
    'digital.delete',
    'digital.web.view',
    'digital.web.preview',
    'digital.web.request',
    'digital.web.create',
    'digital.web.publish',
    'digital.web.delete',
    'digital.web.use',
    'digital.app.view',
    'digital.app.preview',
    'digital.app.request',
    'digital.app.create',
    'digital.app.publish',
    'digital.app.delete',
    'digital.app.use',
    'digital.pricing.manage',
    'digital.activate',
    'digital.orders.view',
    'digital.orders.manage',
    'digital.orders.approve',
  ],
  'audit.cleanup': ['audit.delete'],
};

/** One-way: holder satisfies required read (manage → view), not the reverse. */
export const HOLDER_TO_IMPLIED_READS: Readonly<Record<string, readonly string[]>> = {
  'user.manage': ['user.view'],
  'product.manage': ['product.view'],
  'category.manage': ['category.view'],
  'modifier.manage': ['modifier.view'],
  'inventory.manage': ['inventory.view'],
  'customer.manage': ['customer.view'],
  'invoice.manage': ['invoice.view'],
  'settings.manage': ['settings.view'],
  'localization.manage': ['localization.view'],
  'receipttemplate.manage': ['receipttemplate.view'],
  'benefit.manage': ['benefit.view'],
  'role.manage': ['role.view'],
  'finanzonline.manage': ['finanzonline.view'],
  'payment.take': ['payment.view'],
  'payment.cancel': ['payment.view'],
  'report.export': ['report.view'],
  'audit.export': ['audit.view'],
  'audit.cleanup': ['audit.view'],
  'cart.manage': ['cart.view'],
  'shift.manage': ['shift.view'],
  'shift.open': ['shift.view'],
  'shift.close': ['shift.view'],
  'kitchen.update': ['kitchen.view'],
  'voucher.create': ['voucher.read'],
  'voucher.cancel': ['voucher.read'],
  'voucher.issue': ['voucher.read'],
  'cash_register.manage': ['cash_register.view'],
  'cash_register.decommission': ['cash_register.view'],
  'table.manage': ['table.view'],
  'license.manage': ['license.view'],
  'website.manage': [
    'digital.view',
    'digital.preview',
    'digital.request',
    'digital.web.view',
    'digital.web.preview',
    'digital.web.request',
    'digital.app.view',
    'digital.app.preview',
    'digital.app.request',
  ],
  'digital.view': ['digital.web.view', 'digital.app.view'],
  'digital.preview': [
    'digital.view',
    'digital.web.view',
    'digital.app.view',
    'digital.web.preview',
    'digital.app.preview',
  ],
  'digital.request': [
    'digital.view',
    'digital.web.view',
    'digital.app.view',
    'digital.web.request',
    'digital.app.request',
  ],
  'digital.create': [
    'digital.view',
    'digital.preview',
    'digital.web.view',
    'digital.app.view',
    'digital.web.preview',
    'digital.app.preview',
    'digital.web.create',
    'digital.app.create',
    'digital.web.use',
    'digital.app.use',
  ],
  'digital.publish': [
    'digital.view',
    'digital.web.view',
    'digital.app.view',
    'digital.web.publish',
    'digital.app.publish',
  ],
  'digital.edit': [
    'digital.view',
    'digital.preview',
    'website.manage',
    'digital.web.view',
    'digital.app.view',
  ],
  'digital.delete': ['digital.view', 'digital.web.delete', 'digital.app.delete'],
  'digital.web.create': ['digital.web.view', 'digital.web.preview', 'digital.web.use'],
  'digital.app.create': ['digital.app.view', 'digital.app.preview', 'digital.app.use'],
  'digital.web.publish': ['digital.web.view'],
  'digital.app.publish': ['digital.app.view'],
  'digital.web.use': ['digital.web.view'],
  'digital.app.use': ['digital.app.view'],
  'digital.web.preview': ['digital.web.view'],
  'digital.app.preview': ['digital.app.view'],
  'digital.web.request': ['digital.web.view'],
  'digital.app.request': ['digital.app.view'],
  'digital.pricing.manage': ['digital.view', 'digital.web.view', 'digital.app.view'],
  'digital.activate': ['digital.view', 'digital.web.view', 'digital.app.view'],
  'digital.orders.manage': ['digital.orders.view'],
  'digital.orders.approve': ['digital.orders.view', 'digital.orders.manage'],
  'daily-closing.execute': ['daily-closing.view'],
};

const CHILD_TO_PARENT: Readonly<Record<string, string>> = Object.entries(PARENT_TO_CHILDREN).reduce(
  (acc, [parent, children]) => {
    for (const child of children) {
      if (!(child in acc)) acc[child] = parent;
    }
    return acc;
  },
  {} as Record<string, string>
);

function toSet(effective: Iterable<string>): Set<string> {
  return effective instanceof Set ? effective : new Set(effective);
}

/** True when `required` is granted directly, via holder→read, parent composite, or all children present. */
export function permissionImplied(required: string, effective: Iterable<string>): boolean {
  const set = toSet(effective);
  if (set.size === 0) return false;

  // Compact SuperAdmin JWT emits only system.critical.
  if (set.has('system.critical')) return true;

  if (set.has(required)) return true;

  for (const held of set) {
    const implied = HOLDER_TO_IMPLIED_READS[held];
    if (implied?.includes(required)) return true;
  }

  const children = PARENT_TO_CHILDREN[required];
  if (children?.length && children.every((c) => set.has(c))) return true;

  const parent = CHILD_TO_PARENT[required];
  if (parent && set.has(parent)) return true;

  return false;
}

/**
 * Held permissions that alone satisfy `required` via implication (excludes direct grant).
 * Useful for UI “Implied by …” indicators.
 */
export function findImplicationSources(
  required: string,
  effective: Iterable<string>
): string[] {
  const set = toSet(effective);
  if (set.size === 0 || set.has(required)) return [];

  const sources: string[] = [];
  for (const held of set) {
    if (held === required) continue;
    if (held === 'system.critical') {
      sources.push(held);
      continue;
    }
    if (permissionImplied(required, [held])) {
      sources.push(held);
    }
  }
  return sources.sort((a, b) => a.localeCompare(b));
}

/** Whether `required` is satisfied only via implication (not listed directly in effective). */
export function isPermissionImpliedOnly(
  required: string,
  effective: Iterable<string>
): boolean {
  const set = toSet(effective);
  if (set.has(required)) return false;
  return permissionImplied(required, set);
}
