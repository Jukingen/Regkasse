import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantDataLifecycleState =
  'Active' | 'Grace' | 'Locked' | 'Archived' | 'ExportRequest' | 'Deleted';

export type TenantDataTypeSummary = {
  key: string;
  label: string;
  rowCount: number;
  isRksvRetained: boolean;
  deletedOnPurge: boolean;
};

export type TenantDataDeletionRequest = {
  id: string;
  status: string;
  reason?: string | null;
  requestedAtUtc: string;
  exportCompletedAtUtc?: string | null;
  confirmedAtUtc?: string | null;
  completedAtUtc?: string | null;
  purgeEligibleAtUtc?: string | null;
  executedVia?: string | null;
  confirmationWaitDays?: number;
};

export type TenantDataManagementSummary = {
  tenantId: string;
  tenantSlug: string;
  tenantName: string;
  lifecycleState: TenantDataLifecycleState | string;
  licenseValidUntilUtc?: string | null;
  daysOverdue: number;
  isInGracePeriod?: boolean;
  gracePeriodRemainingDays?: number;
  isLocked?: boolean;
  isArchived?: boolean;
  customerDataPurgedAtUtc?: string | null;
  rksvRetentionYears: number;
  rksvRetentionNote: string;
  canExport: boolean;
  canRequestDeletion: boolean;
  canConfirmDeletion?: boolean;
  canExecutePurge?: boolean;
  dataTypes: TenantDataTypeSummary[];
  latestDeletionRequest?: TenantDataDeletionRequest | null;
  retention?: {
    retentionYears: number;
    rksvData: {
      paymentDetailsCount: number;
      oldestPaymentDate?: string | null;
      retentionUntil?: string | null;
    };
    nonRksvData: {
      productsCount: number;
      customersCount: number;
      canBeDeleted: boolean;
    };
  } | null;
};

export type DeletionResult = {
  succeeded: boolean;
  error?: string | null;
  errorCode?: string | null;
  requestId?: string | null;
  tenantId?: string | null;
  deletedCounts?: Record<string, number> | null;
};

export async function getTenantDataManagementSummary(
  tenantId: string
): Promise<TenantDataManagementSummary> {
  const { data } = await AXIOS_INSTANCE.get<TenantDataManagementSummary>(
    `/api/admin/tenants/${tenantId}/data-management`
  );
  return data;
}

export async function downloadTenantDataExport(tenantId: string): Promise<Blob> {
  const { data } = await AXIOS_INSTANCE.get<Blob>(
    `/api/admin/tenants/${tenantId}/data-management/export`,
    { responseType: 'blob' }
  );
  return data;
}

export async function requestTenantDataDeletion(
  tenantId: string,
  reason?: string
): Promise<TenantDataDeletionRequest> {
  const { data } = await AXIOS_INSTANCE.post<TenantDataDeletionRequest>(
    `/api/admin/tenants/${tenantId}/data-management/deletion-request`,
    { reason: reason?.trim() || undefined }
  );
  return data;
}

export async function confirmTenantDataDeletion(
  tenantId: string,
  requestId: string
): Promise<TenantDataDeletionRequest> {
  const { data } = await AXIOS_INSTANCE.post<TenantDataDeletionRequest>(
    `/api/admin/tenants/${tenantId}/data-management/deletion-request/${requestId}/confirm`
  );
  return data;
}

export async function executeTenantDataPurge(
  tenantId: string,
  requestId: string
): Promise<DeletionResult> {
  const { data } = await AXIOS_INSTANCE.post<DeletionResult>(
    `/api/admin/tenants/${tenantId}/data-management/deletion-request/${requestId}/execute`
  );
  return data;
}

export type DataRightsRequestType = 'view' | 'export' | 'delete';

export type DataRightsRequestTypeCatalogItem = {
  type: DataRightsRequestType | string;
  description: string;
  approval: string;
  processingTime: string;
  approvalMode: 'auto' | 'manual' | string;
  maxProcessingHours?: number | null;
  confirmationWaitDays?: number | null;
};

export type TenantDataRightsRequest = {
  id: string;
  tenantId: string;
  requestType: DataRightsRequestType | string;
  status: string;
  approvalMode: string;
  reason?: string | null;
  requestedAtUtc: string;
  approvedAtUtc?: string | null;
  processingDeadlineUtc?: string | null;
  readyAtUtc?: string | null;
  completedAtUtc?: string | null;
  artifactFileName?: string | null;
  artifactByteSize?: number | null;
  downloadLink?: string | null;
  downloadExpiresAtUtc?: string | null;
  canDownload: boolean;
  canConfirm: boolean;
  canExecute: boolean;
  linkedDeletionRequestId?: string | null;
  linkedDeletionRequest?: TenantDataDeletionRequest | null;
  viewSummary?: TenantDataManagementSummary | null;
  errorMessage?: string | null;
  confirmationWaitDays?: number | null;
};

export async function listDataRightsRequestTypes(
  tenantId: string
): Promise<DataRightsRequestTypeCatalogItem[]> {
  const { data } = await AXIOS_INSTANCE.get<DataRightsRequestTypeCatalogItem[]>(
    `/api/admin/tenants/${tenantId}/data-management/request-types`
  );
  return data;
}

export async function listDataRightsRequests(tenantId: string): Promise<TenantDataRightsRequest[]> {
  const { data } = await AXIOS_INSTANCE.get<TenantDataRightsRequest[]>(
    `/api/admin/tenants/${tenantId}/data-management/requests`
  );
  return data;
}

export type DataAccessResult = {
  succeeded: boolean;
  isPending: boolean;
  error?: string | null;
  errorCode?: string | null;
  request?: {
    id: string;
    tenantId: string;
    type: DataRightsRequestType | string | number;
    status: string;
    requestedAt: string;
    requestedBy?: string | null;
    approvedAt?: string | null;
    reason?: string | null;
  } | null;
  rights?: TenantDataRightsRequest | null;
};

export async function createDataRightsRequest(
  tenantId: string,
  type: DataRightsRequestType,
  reason?: string
): Promise<TenantDataRightsRequest> {
  const { data } = await AXIOS_INSTANCE.post<DataAccessResult>(
    `/api/admin/tenants/${tenantId}/data-management/requests`,
    { type, reason: reason?.trim() || undefined },
    // Delete returns HTTP 202 Accepted with the same payload shape.
    { validateStatus: (status) => status === 200 || status === 202 }
  );

  if (!data.succeeded || !data.rights) {
    throw new Error(data.error ?? 'Data access request failed');
  }

  return data.rights;
}

export async function downloadDataRightsExport(tenantId: string, requestId: string): Promise<Blob> {
  const { data } = await AXIOS_INSTANCE.get<Blob>(
    `/api/admin/tenants/${tenantId}/data-management/requests/${requestId}/download`,
    { responseType: 'blob' }
  );
  return data;
}

export async function confirmDataRightsDelete(
  tenantId: string,
  requestId: string
): Promise<TenantDataRightsRequest> {
  const { data } = await AXIOS_INSTANCE.post<TenantDataRightsRequest>(
    `/api/admin/tenants/${tenantId}/data-management/requests/${requestId}/confirm`
  );
  return data;
}

export async function executeDataRightsDelete(
  tenantId: string,
  requestId: string
): Promise<DeletionResult> {
  const { data } = await AXIOS_INSTANCE.post<DeletionResult>(
    `/api/admin/tenants/${tenantId}/data-management/requests/${requestId}/execute`
  );
  return data;
}
