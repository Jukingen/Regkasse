import { AXIOS_INSTANCE } from '@/lib/axios';
import type { MandantLicenseOverviewKind } from '@/features/license/utils/mandantLicenseOverviewStatus';

export type TenantLicenseOverviewItem = {
    tenantId: string;
    tenantName: string;
    tenantSlug: string;
    licenseKey: string | null;
    validUntilUtc: string | null;
    status: MandantLicenseOverviewKind;
    hasOwnerAdmin: boolean;
    createdAt: string;
};

export const tenantLicenseOverviewQueryKey = ['admin', 'tenants', 'license-overview'] as const;

/** GET /api/admin/tenants/license-overview — Super Admin mandant license inventory. */
export async function getTenantLicenseOverview(): Promise<TenantLicenseOverviewItem[]> {
    const { data } = await AXIOS_INSTANCE.get<TenantLicenseOverviewApiDto[]>(
        '/api/admin/tenants/license-overview',
    );

    return data.map(mapTenantLicenseOverviewItem);
}

type TenantLicenseOverviewApiDto = {
    tenantId: string;
    tenantName: string;
    tenantSlug: string;
    licenseKey?: string | null;
    validUntilUtc?: string | null;
    status: string;
    hasOwnerAdmin: boolean;
    createdAt: string;
};

function mapOverviewStatus(status: string): MandantLicenseOverviewKind {
    switch (status) {
        case 'active':
        case 'expiring_soon':
        case 'expired':
        case 'trial':
        case 'no_license':
            return 'none';
        default:
            return 'none';
    }
}

function mapTenantLicenseOverviewItem(row: TenantLicenseOverviewApiDto): TenantLicenseOverviewItem {
    return {
        tenantId: row.tenantId,
        tenantName: row.tenantName,
        tenantSlug: row.tenantSlug,
        licenseKey: row.licenseKey ?? null,
        validUntilUtc: row.validUntilUtc ?? null,
        status: mapOverviewStatus(row.status),
        hasOwnerAdmin: row.hasOwnerAdmin,
        createdAt: row.createdAt,
    };
}
