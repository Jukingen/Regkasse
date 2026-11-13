export type TenantMembershipRef = {
  tenantId: string;
};

/**
 * Resolves which tenant context to use for PUT /api/admin/tenants/{tenantId}/users/{userId}/role.
 * Prefers the active tenant when the user belongs to it; otherwise falls back to a single membership or first match.
 */
export function resolveRoleChangeTenantId(
  memberships: readonly TenantMembershipRef[],
  preferredTenantId: string | null | undefined
): string | undefined {
  const preferred = preferredTenantId?.trim();
  if (preferred && memberships.some((m) => m.tenantId === preferred)) {
    return preferred;
  }
  if (memberships.length === 1) {
    return memberships[0].tenantId;
  }
  if (memberships.length > 1) {
    return memberships[0]?.tenantId;
  }
  return preferred || undefined;
}
