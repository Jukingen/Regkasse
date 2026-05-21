/** Super-admin unified user management route. */
export const ADMIN_USERS_PAGE_PATH = '/admin/users';

export function buildAdminUsersPageHref(tenantId?: string | null): string {
    if (!tenantId?.trim()) {
        return ADMIN_USERS_PAGE_PATH;
    }
    return `${ADMIN_USERS_PAGE_PATH}?tenantId=${encodeURIComponent(tenantId.trim())}`;
}

/** Reads `tenantId` from URL; accepts legacy `tenant` for backward compatibility. */
export function readTenantIdFromSearchParams(
    searchParams: Pick<URLSearchParams, 'get'>,
): string | undefined {
    const id = searchParams.get('tenantId')?.trim() || searchParams.get('tenant')?.trim();
    return id || undefined;
}

const FILTER_ALL = '';
const FILTER_PLATFORM = '__platform__';

/** Maps URL query to unified list filter value (`''` | `__platform__` | tenant UUID). */
export function resolveAdminUsersTenantFilterFromSearchParams(
    searchParams: Pick<URLSearchParams, 'get'>,
): string {
    if (searchParams.get('filter') === 'platform') {
        return FILTER_PLATFORM;
    }
    return readTenantIdFromSearchParams(searchParams) || FILTER_ALL;
}

export { FILTER_ALL as ADMIN_USERS_FILTER_ALL, FILTER_PLATFORM as ADMIN_USERS_FILTER_PLATFORM };
