const CANONICAL_ROLE_NAMES = new Set([
    'SuperAdmin',
    'Manager',
    'Cashier',
    'Waiter',
    'Kitchen',
    'ReportViewer',
    'Accountant',
]);

export function formatRoleDisplayLabel(
    t: (key: string, options?: Record<string, string | number>) => string,
    roleName: string,
): string {
    return CANONICAL_ROLE_NAMES.has(roleName)
        ? t(`users.roles.displayNames.${roleName}`)
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
