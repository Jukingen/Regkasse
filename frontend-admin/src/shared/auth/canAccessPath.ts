import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

/** True when JWT permissions satisfy `ROUTE_PERMISSIONS` for the path (same rule as `PermissionRouteGuard`). */
export function canAccessPath(path: string, permissions: string[] | undefined): boolean {
    const required = getRequiredPermissionForPath(path);
    if (required === undefined) return false;
    if (!permissions?.length) return false;
    const arr = Array.isArray(required) ? required : [required];
    if (arr.length === 0) return permissions.length > 0;
    return arr.some((p) => permissions.includes(p));
}
