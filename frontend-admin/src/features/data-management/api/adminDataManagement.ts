import { AXIOS_INSTANCE } from '@/lib/axios';
import type {
  TenantDataLifecycleState,
  TenantDataManagementSummary,
} from '@/features/data-management/api/tenantDataManagement';

export type TenantDataManagementOverviewItem = {
  tenantId: string;
  tenantSlug: string;
  tenantName: string;
  lifecycleState: TenantDataLifecycleState | string;
  licenseValidUntilUtc?: string | null;
  daysOverdue: number;
  isInGracePeriod: boolean;
  gracePeriodRemainingDays: number;
  isLocked: boolean;
  isArchived: boolean;
  customerDataPurgedAtUtc?: string | null;
  hasPendingDeletionRequest: boolean;
  deletionRequestStatus?: string | null;
  deletionRequestedAtUtc?: string | null;
  oldestRksvPaymentDate?: string | null;
  rksvRetentionUntil?: string | null;
  rksvPaymentCount: number;
};

export type TenantDataManagementOverview = {
  items: TenantDataManagementOverviewItem[];
  totalTenants: number;
  inGraceCount: number;
  lockedCount: number;
  pendingDeletionRequestCount: number;
  purgedCount: number;
};

export type RksvRetentionReport = NonNullable<TenantDataManagementSummary['retention']>;

export async function listDataManagementOverview(): Promise<TenantDataManagementOverview> {
  const { data } = await AXIOS_INSTANCE.get<TenantDataManagementOverview>(
    '/api/admin/data-management',
  );
  return data;
}
