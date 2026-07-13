export const CANONICAL_ROLE_NAMES = [
    'SuperAdmin',
    'Manager',
    'Cashier',
    'Waiter',
    'Kitchen',
    'ReportViewer',
    'Accountant',
] as const;

const CANONICAL_ROLE_NAME_SET = new Set<string>(CANONICAL_ROLE_NAMES);

export function isCanonicalRoleName(roleName: string): roleName is (typeof CANONICAL_ROLE_NAMES)[number] {
    return CANONICAL_ROLE_NAME_SET.has(roleName);
}

/** Form labels, selects, role-management workspace — full display names. */
export function formatRoleDisplayLabel(
    t: (key: string, options?: Record<string, string | number>) => string,
    roleName: string,
): string {
    return isCanonicalRoleName(roleName)
        ? t(`users.roles.displayNames.${roleName}`)
        : roleName;
}

/** Table badges and compact role chips — shorter badge labels. */
export function formatRoleBadgeLabel(
    t: (key: string, options?: Record<string, string | number>) => string,
    roleName: string,
): string {
    return isCanonicalRoleName(roleName)
        ? t(`users.roles.badgeLabels.${roleName}`)
        : roleName;
}

export function buildRoleSelectOptions(
    roles: readonly string[],
    t: (key: string, options?: Record<string, string | number>) => string,
): { value: string; label: string }[] {
    return roles.map((role) => ({
        value: role,
        label: formatRoleDisplayLabel(t, role),
    }));
}
