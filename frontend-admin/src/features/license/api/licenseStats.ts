import { useQuery } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';

export interface LicenseActivity {
    timestampUtc: string;
    licenseKeyMasked: string;
    machineFingerprintShort: string | null;
    action: string;
    sourceCode: string;
}

export interface LicenseDashboardStats {
    activeTenantLicenses: number;
    expiringTenantLicenses: number;
    expiredTenantLicenses: number;
    activeDeploymentLicenses: number;
    expiringDeploymentLicenses: number;
    expiredDeploymentLicenses: number;
    activatedDevices: number;
    recentActivities: LicenseActivity[];
}

export const licenseDashboardStatsQueryKey = ['license', 'dashboard-stats'] as const;

export const getLicenseDashboardStats = async (): Promise<LicenseDashboardStats> => {
    const { data } = await AXIOS_INSTANCE.get<LicenseDashboardStats>('/api/admin/license/dashboard-stats');
    return data;
};

export const useLicenseDashboardStats = (options?: { enabled?: boolean }) => {
    return useQuery({
        queryKey: licenseDashboardStatsQueryKey,
        queryFn: getLicenseDashboardStats,
        refetchInterval: 60_000,
        enabled: options?.enabled !== false,
    });
};
