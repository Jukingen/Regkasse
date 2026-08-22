export const LIMIT_DASHBOARD_PATH = '/admin/limits/dashboard';
export const LIMIT_DASHBOARD_ALL_TENANTS_VALUE = '__all__';

export type LimitDashboardSearch = {
  allTenants: boolean;
  tenantId?: string;
  registerId?: string;
};

export function parseLimitDashboardSearch(
  searchParams: Pick<URLSearchParams, 'get'>
): LimitDashboardSearch {
  const allRaw = searchParams.get('allTenants')?.trim().toLowerCase();
  const allTenants = allRaw === '1' || allRaw === 'true';
  const tenantId = searchParams.get('tenantId')?.trim() || undefined;
  const registerId = searchParams.get('registerId')?.trim() || undefined;
  return {
    allTenants,
    tenantId: allTenants ? undefined : tenantId,
    registerId: allTenants ? undefined : registerId,
  };
}

export function buildLimitDashboardHref(options: LimitDashboardSearch): string {
  const params = new URLSearchParams();
  if (options.allTenants) {
    params.set('allTenants', '1');
  } else if (options.tenantId) {
    params.set('tenantId', options.tenantId);
  }
  if (!options.allTenants && options.registerId) {
    params.set('registerId', options.registerId);
  }
  const query = params.toString();
  return query ? `${LIMIT_DASHBOARD_PATH}?${query}` : LIMIT_DASHBOARD_PATH;
}

export function formatLimitDashboardPersonName(user: {
  firstName?: string | null;
  lastName?: string | null;
  userName?: string | null;
  email?: string | null;
} | null | undefined): string {
  if (!user) return '—';
  const full = `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim();
  return full || user.userName || user.email || '—';
}
