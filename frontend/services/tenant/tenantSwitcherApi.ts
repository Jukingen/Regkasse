import { apiClient } from '../api/config';

/** Row from GET /api/tenants/switcher (aligned with FA AdminTenantListItem). */
export type TenantSwitcherListItem = {
  id: string;
  name: string;
  slug: string;
  status: string;
  isActive: boolean;
};

/** Active tenants for dev header switcher (excludes soft-deleted unless includeDeleted). */
export async function fetchTenantSwitcherList(
  includeDeleted = false,
): Promise<TenantSwitcherListItem[]> {
  return apiClient.get<TenantSwitcherListItem[]>('/tenants/switcher', {
    params: { includeDeleted },
  });
}
