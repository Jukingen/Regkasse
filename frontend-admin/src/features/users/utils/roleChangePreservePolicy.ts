import { isPlatformUserRole } from '@/features/users/utils/userScope';

/** Whether the preserve-permissions checkbox can be offered in the role-change modal. */
export type RoleChangePreserveAvailability =
    | 'available'
    | 'no_previous_role'
    | 'superadmin_source'
    | 'same_role'
    | 'unavailable';

export function normalizeRoleName(role: string | undefined | null): string {
    return role?.trim() ?? '';
}

export function isSameRoleName(
    previousRole: string | undefined | null,
    newRole: string | undefined | null,
): boolean {
    const previous = normalizeRoleName(previousRole);
    const next = normalizeRoleName(newRole);
    if (!previous || !next) return false;
    return previous.toLowerCase() === next.toLowerCase();
}

export function getRoleChangePreserveAvailability(
    previousRole: string | undefined | null,
    newRole: string | undefined | null,
): RoleChangePreserveAvailability {
    const previous = normalizeRoleName(previousRole);
    const next = normalizeRoleName(newRole);

    if (!next) return 'unavailable';
    if (!previous) return 'no_previous_role';
    if (isSameRoleName(previous, next)) return 'same_role';
    if (isPlatformUserRole(previous)) return 'superadmin_source';
    return 'available';
}

export function shouldPromptRoleChange(
    previousRole: string | undefined | null,
    newRole: string | undefined | null,
): boolean {
    const availability = getRoleChangePreserveAvailability(previousRole, newRole);
    return availability !== 'same_role' && availability !== 'unavailable' && Boolean(normalizeRoleName(newRole));
}

export function canOfferPreservePermissions(
    previousRole: string | undefined | null,
    newRole: string | undefined | null,
    hasTenantContext = true,
): boolean {
    if (!hasTenantContext) return false;
    return getRoleChangePreserveAvailability(previousRole, newRole) === 'available';
}
