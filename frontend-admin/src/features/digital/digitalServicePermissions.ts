import { PERMISSIONS, hasAnyPermission, type UserWithPermissions } from '@/shared/auth/permissions';
import type { TenantDigitalServiceRow } from '@/features/digital-services/api/tenantDigitalServicesApi';

/** Permissions that unlock Mandanten digital-service portal (view). */
export const DIGITAL_SERVICE_VIEW_PERMISSIONS = [
  PERMISSIONS.DIGITAL_VIEW,
  PERMISSIONS.DIGITAL_PREVIEW,
  PERMISSIONS.DIGITAL_REQUEST,
  PERMISSIONS.DIGITAL_CREATE,
  PERMISSIONS.DIGITAL_MANAGE,
  PERMISSIONS.DIGITAL_WEB_VIEW,
  PERMISSIONS.DIGITAL_APP_VIEW,
  PERMISSIONS.WEBSITE_MANAGE,
] as const;

/** Create / generate (Super Admin). */
export const DIGITAL_CREATE_PERMISSIONS = [
  PERMISSIONS.DIGITAL_CREATE,
  PERMISSIONS.DIGITAL_MANAGE,
  PERMISSIONS.DIGITAL_WEB_CREATE,
  PERMISSIONS.DIGITAL_APP_CREATE,
] as const;

/** Preview. */
export const DIGITAL_PREVIEW_PERMISSIONS = [
  PERMISSIONS.DIGITAL_PREVIEW,
  PERMISSIONS.DIGITAL_CREATE,
  PERMISSIONS.DIGITAL_MANAGE,
  PERMISSIONS.WEBSITE_MANAGE,
] as const;

/** Request creation. */
export const DIGITAL_REQUEST_PERMISSIONS = [
  PERMISSIONS.DIGITAL_REQUEST,
  PERMISSIONS.DIGITAL_MANAGE,
  PERMISSIONS.WEBSITE_MANAGE,
] as const;

export function canAccessDigitalServices(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_SERVICE_VIEW_PERMISSIONS]);
}

/** @deprecated Prefer canCreateDigitalWeb — generate is SuperAdmin-only. */
export function canUseDigitalWeb(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  return canCreateDigitalWeb(user, isSuperAdmin);
}

/** @deprecated Prefer canCreateDigitalApp — generate is SuperAdmin-only. */
export function canUseDigitalApp(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  return canCreateDigitalApp(user, isSuperAdmin);
}

export function canCreateDigitalWeb(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_CREATE_PERMISSIONS]);
}

export function canCreateDigitalApp(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_CREATE_PERMISSIONS]);
}

export function canPreviewDigitalWeb(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_PREVIEW_PERMISSIONS]);
}

export function canPreviewDigitalApp(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_PREVIEW_PERMISSIONS]);
}

export function canRequestDigitalWeb(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_REQUEST_PERMISSIONS]);
}

export function canRequestDigitalApp(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
): boolean {
  if (isSuperAdmin) return true;
  return hasAnyPermission(user, [...DIGITAL_REQUEST_PERMISSIONS]);
}

/**
 * True when at least one digital surface is available for the tenant
 * (`isEnabled && isActive` on website or app). Missing status → treat as enabled
 * (optimistic) so Super Admin / loading paths do not hard-block.
 */
export function isAnyDigitalServiceAvailable(
  row: TenantDigitalServiceRow | null | undefined,
): boolean {
  if (!row) return true;
  return Boolean(row.website?.isAvailable || row.app?.isAvailable);
}

/** Website generate allowed for this user + tenant status. */
export function canGenerateDigitalWebsite(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
  row: TenantDigitalServiceRow | null | undefined,
): boolean {
  if (!canCreateDigitalWeb(user, isSuperAdmin)) return false;
  if (!row) return true;
  return Boolean(row.website?.isAvailable);
}

/** App generate allowed for this user + tenant status. */
export function canGenerateDigitalApp(
  user: UserWithPermissions | null | undefined,
  isSuperAdmin: boolean,
  row: TenantDigitalServiceRow | null | undefined,
): boolean {
  if (!canCreateDigitalApp(user, isSuperAdmin)) return false;
  if (!row) return true;
  return Boolean(row.app?.isAvailable);
}
