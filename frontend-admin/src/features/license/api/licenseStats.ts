import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { AXIOS_INSTANCE } from '@/lib/axios';

/** Matches backend `LicenseActivityDto` from GET /api/admin/license/dashboard-stats. */
export interface LicenseActivity {
  timestamp: string;
  licenseKey: string;
  machineHash: string;
  action: string;
  userEmail: string;
}

export interface LicenseDashboardStats {
  totalTenants: number;
  activeTenantLicenses: number;
  expiringTenantLicenses: number;
  expiredTenantLicenses: number;
  graceTenantLicenses: number;
  lockedTenantLicenses: number;
  activeDeploymentLicenses: number;
  expiringDeploymentLicenses: number;
  expiredDeploymentLicenses: number;
  activatedDevices: number;
  recentActivities: LicenseActivity[];
}

type LicenseActivityApiRow = Partial<LicenseActivity> & {
  timestampUtc?: string;
  licenseKeyMasked?: string;
  machineFingerprintShort?: string | null;
  sourceCode?: string;
  userEmail?: string;
};

function normalizeLicenseActivity(row: LicenseActivityApiRow): LicenseActivity {
  return {
    timestamp: row.timestamp ?? row.timestampUtc ?? '',
    licenseKey: row.licenseKey ?? row.licenseKeyMasked ?? '',
    machineHash: row.machineHash ?? row.machineFingerprintShort ?? '',
    action: row.action ?? row.sourceCode ?? '',
    userEmail: row.userEmail ?? '',
  };
}

export const licenseDashboardStatsQueryKey = ['license', 'dashboard-stats'] as const;

export const getLicenseDashboardStats = async (): Promise<LicenseDashboardStats> => {
  const { data } = await AXIOS_INSTANCE.get<LicenseDashboardStats>(
    '/api/admin/license/dashboard-stats'
  );
  return {
    ...data,
    recentActivities: (data.recentActivities ?? []).map((row) =>
      normalizeLicenseActivity(row as LicenseActivityApiRow)
    ),
  };
};

export const useLicenseDashboardStats = (options?: { enabled?: boolean }) => {
  return useAuthorizedQuery({
    queryKey: licenseDashboardStatsQueryKey,
    queryFn: getLicenseDashboardStats,
    requiredRole: 'SuperAdmin',
    refetchInterval: 60_000,
    enabled: options?.enabled !== false,
  });
};
