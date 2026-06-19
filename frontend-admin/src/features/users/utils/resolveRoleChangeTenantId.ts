export type RoleChangeTenantMembership = {
    tenantId: string;
};

/**
 * Pick tenant for PUT /api/admin/tenants/{tenantId}/users/{userId}/role (preserve flag).
 * Prefers active admin tenant when the user belongs to it; otherwise first membership.
 */
export function resolveRoleChangeTenantId(
    memberships: RoleChangeTenantMembership[],
    currentTenantId?: string | null,
): string | undefined {
    const normalizedCurrent = currentTenantId?.trim();
    if (normalizedCurrent) {
        const match = memberships.find((m) => m.tenantId === normalizedCurrent);
        if (match) return match.tenantId;
        if (memberships.length === 0) return normalizedCurrent;
    }
    return memberships[0]?.tenantId ?? normalizedCurrent ?? undefined;
}
