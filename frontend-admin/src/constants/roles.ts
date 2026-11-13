/**
 * Core admin role constants and hierarchy helpers.
 * Aligned with backend `Roles.cs` (SuperAdmin, Manager, Cashier).
 *
 * Prefer permission checks (`@/hooks/usePermissions`) for UI gates;
 * use role hierarchy only when a minimum role level is required.
 */

export const ROLES = {
  SUPER_ADMIN: 'SuperAdmin',
  MANAGER: 'Manager',
  CASHIER: 'Cashier',
} as const;

export type Role = (typeof ROLES)[keyof typeof ROLES];

export const ROLE_HIERARCHY: Record<Role, number> = {
  [ROLES.SUPER_ADMIN]: 3,
  [ROLES.MANAGER]: 2,
  [ROLES.CASHIER]: 1,
};

export const hasMinRole = (userRole: string | undefined, minRole: string): boolean => {
  if (!userRole) return false;
  return (ROLE_HIERARCHY[userRole as Role] ?? 0) >= (ROLE_HIERARCHY[minRole as Role] ?? 0);
};
