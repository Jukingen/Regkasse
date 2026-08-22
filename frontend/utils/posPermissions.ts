/**
 * POS operation flags: JWT claims AND POS role (Cashier vs Waiter).
 * SuperAdmin (role or compact `system.critical` JWT) bypasses individual checks.
 *
 * Role equality (sketch): Waiter may take/view orders but not pay or open/close shift.
 * Cashier keeps order-taking — the Kassa tab is their order UI (`canTakeOrders` is not Waiter-only).
 */

export type PosPermissionUser = {
  role?: string;
  roles?: string[];
  permissions?: string[];
};

export type PosPermissions = {
  isCashier: boolean;
  isWaiter: boolean;
  canMakePayment: boolean;
  canOpenShift: boolean;
  canCloseShift: boolean;
  canViewOrders: boolean;
  canTakeOrders: boolean;
  canCreateSonderbeleg: boolean;
};

const SYSTEM_CRITICAL = 'system.critical';

const RKSV_CREATE_PERMISSIONS = [
  'rksv.nullbeleg.create',
  'rksv.startbeleg.create',
  'rksv.monatsbeleg.create',
  'rksv.jahresbeleg.create',
  'rksv.schlussbeleg.create',
] as const;

const ALL_DENIED: PosPermissions = {
  isCashier: false,
  isWaiter: false,
  canMakePayment: false,
  canOpenShift: false,
  canCloseShift: false,
  canViewOrders: false,
  canTakeOrders: false,
  canCreateSonderbeleg: false,
};

function hasNamedRole(user: PosPermissionUser, role: string): boolean {
  if (user.role === role) return true;
  return (user.roles ?? []).some((r) => r === role);
}

function permissionSet(user: PosPermissionUser): Set<string> {
  return new Set((user.permissions ?? []).map((x) => x.toLowerCase()));
}

function hasClaim(granted: Set<string>, permission: string): boolean {
  return granted.has(permission.toLowerCase());
}

/**
 * Resolve POS UI flags from the authenticated user.
 */
export function resolvePosPermissions(
  user: PosPermissionUser | null | undefined
): PosPermissions {
  if (!user) return ALL_DENIED;

  const isCashier = hasNamedRole(user, 'Cashier');
  const isWaiter = hasNamedRole(user, 'Waiter');
  const granted = permissionSet(user);
  const isPrivileged = hasNamedRole(user, 'SuperAdmin') || hasClaim(granted, SYSTEM_CRITICAL);

  if (isPrivileged) {
    return {
      isCashier,
      isWaiter,
      canMakePayment: true,
      canOpenShift: true,
      canCloseShift: true,
      canViewOrders: true,
      canTakeOrders: true,
      canCreateSonderbeleg: true,
    };
  }

  const posFloorStaff = isCashier || isWaiter;

  return {
    isCashier,
    isWaiter,
    canMakePayment: isCashier && hasClaim(granted, 'payment.take'),
    canOpenShift: isCashier && hasClaim(granted, 'shift.open'),
    canCloseShift: isCashier && hasClaim(granted, 'shift.close'),
    canViewOrders: posFloorStaff && hasClaim(granted, 'order.view'),
    canTakeOrders: posFloorStaff && hasClaim(granted, 'order.create'),
    canCreateSonderbeleg: RKSV_CREATE_PERMISSIONS.some((key) => hasClaim(granted, key)),
  };
}
