import { isPlatformUserRole } from '@/features/users/utils/userScope';

export function isRoleChange(
    previousRole: string | undefined | null,
    newRole: string | undefined | null,
): boolean {
    const previous = previousRole?.trim() ?? '';
    const next = newRole?.trim() ?? '';
    return Boolean(next) && previous.toLowerCase() !== next.toLowerCase();
}

/** Tenant role API applies to business staff with a known previous role. */
export function shouldUseTenantRoleChangeApi(
    previousRole: string | undefined | null,
    newRole: string | undefined | null,
): boolean {
    if (!previousRole?.trim()) {
        return false;
    }
    if (isPlatformUserRole(previousRole) || isPlatformUserRole(newRole)) {
        return false;
    }
    return isRoleChange(previousRole, newRole);
}
