/**
 * Staff hub policy — maps staff UX to existing permission keys (no staff.* catalog).
 * Backend: user.view (list + lifecycle audit), report.view (compliance report), user.manage (CRUD).
 */
import { useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';

import { PERMISSIONS, type UserWithPermissions, hasPermission } from './permissions';

export interface StaffPolicy {
  /** Staff list / detail read (staff.view → user.view). */
  canView: boolean;
  /** User lifecycle audit timeline (staff.view_activity → user.view on GET /api/AuditLog/user/{id}). */
  canViewActivity: boolean;
  /** Compliance user-activity report panel (report.view). */
  canViewActivityReport: boolean;
  /** Tenant membership read in detail drawer (user.view after backend alignment). */
  canViewTenantMemberships: boolean;
  /** Create/edit/deactivate users (staff.manage → user.manage). */
  canManage: boolean;
}

export function getStaffPolicy(permissions?: string[]): StaffPolicy {
  const user: UserWithPermissions | null =
    permissions && permissions.length > 0 ? { permissions } : null;

  const canView = hasPermission(user, PERMISSIONS.USER_VIEW);
  const canManage = hasPermission(user, PERMISSIONS.USER_MANAGE);

  return {
    canView,
    canViewActivity: canView,
    canViewActivityReport: hasPermission(user, PERMISSIONS.REPORT_VIEW),
    canViewTenantMemberships: canView,
    canManage,
  };
}

export function useStaffPolicy(): StaffPolicy {
  const { user } = useAuth();
  const permissions = (user as { permissions?: string[] } | undefined)?.permissions;
  return useMemo(() => getStaffPolicy(permissions), [permissions]);
}
