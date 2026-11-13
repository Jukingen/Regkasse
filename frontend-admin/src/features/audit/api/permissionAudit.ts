import { AXIOS_INSTANCE } from '@/lib/axios';

export interface PermissionAuditEntry {
  id: string;
  timestamp: string;
  actorUserId: string;
  actorName: string;
  actorEmail: string;
  action: 'created' | 'updated' | 'deleted' | 'reverted';
  roleId: string;
  roleName: string;
  permissionKey: string;
  oldValue: string | null;
  newValue: string | null;
  reason?: string;
  ipAddress?: string;
}

export type PaginatedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type GetPermissionAuditLogsParams = {
  roleId?: string;
  /** Role name filter when Identity role id is unavailable. */
  roleName?: string;
  permissionKey?: string;
  actorUserId?: string;
  fromDate?: string;
  toDate?: string;
  page: number;
  pageSize: number;
};

/** Alias used by `usePermissionAudit`. */
export type AuditParams = GetPermissionAuditLogsParams;

export async function getPermissionAuditLogs(
  params: GetPermissionAuditLogsParams
): Promise<PaginatedResponse<PermissionAuditEntry>> {
  const { data } = await AXIOS_INSTANCE.get<PaginatedResponse<PermissionAuditEntry>>(
    '/api/admin/audit/permissions',
    { params }
  );
  return data;
}

export type RevertPermissionAuditRequest = {
  reason?: string;
  /** When true, proceed even if newer role permission audits exist. */
  force?: boolean;
};

export type RevertPermissionAuditResponse = {
  success: boolean;
  revertedAuditId?: string;
  newAuditId?: string;
  newerChangesCount?: number;
  warningNewerChanges?: boolean;
  message?: string;
};

export async function revertPermissionAudit(
  auditId: string,
  body: RevertPermissionAuditRequest = {}
): Promise<RevertPermissionAuditResponse> {
  const { data } = await AXIOS_INSTANCE.post<RevertPermissionAuditResponse>(
    `/api/admin/audit/permissions/${encodeURIComponent(auditId)}/revert`,
    body
  );
  return data;
}

export type AddPermissionAuditNoteRequest = {
  note: string;
};

export async function addPermissionAuditNote(
  auditId: string,
  body: AddPermissionAuditNoteRequest
): Promise<{ success: boolean }> {
  const { data } = await AXIOS_INSTANCE.post<{ success: boolean }>(
    `/api/admin/audit/permissions/${encodeURIComponent(auditId)}/note`,
    body
  );
  return data;
}

export type PermissionAuditNamedCount = {
  key: string;
  label: string;
  count: number;
};

export type PermissionAuditDailyCount = {
  date: string;
  count: number;
};

export type PermissionAuditReport = {
  fromUtc: string;
  toUtc: string;
  totalChanges: number;
  byAction: Record<string, number>;
  byDate: PermissionAuditDailyCount[];
  topActors: PermissionAuditNamedCount[];
  topPermissions: PermissionAuditNamedCount[];
  topRoles: PermissionAuditNamedCount[];
  criticalCount: number;
  uniqueActors: number;
  uniquePermissions: number;
};

export type PermissionAuditReportParams = {
  roleId?: string;
  roleName?: string;
  permissionKey?: string;
  actorUserId?: string;
  fromDate?: string;
  toDate?: string;
};

export async function getPermissionAuditReport(
  params: PermissionAuditReportParams = {}
): Promise<PermissionAuditReport> {
  const { data } = await AXIOS_INSTANCE.get<PermissionAuditReport>(
    '/api/admin/audit/permissions/report',
    { params }
  );
  return data;
}

export type PermissionAccessRow = {
  subjectType: 'role' | 'user' | string;
  subjectId: string;
  subjectName: string;
  permissionKey: string;
  accessState: string;
  lastReviewedAtUtc?: string | null;
  isStale: boolean;
  expiresAtUtc?: string | null;
  isExpired: boolean;
};

export type PermissionCompliance = {
  generatedAtUtc: string;
  staleDaysThreshold: number;
  lastPermissionReviewAtUtc?: string | null;
  rolePermissionCount: number;
  activeOverrideCount: number;
  expiredOverrideCount: number;
  staleSubjectCount: number;
  accessMatrix: PermissionAccessRow[];
  expiredOrStale: PermissionAccessRow[];
};

export async function getPermissionCompliance(
  staleDays = 90
): Promise<PermissionCompliance> {
  const { data } = await AXIOS_INSTANCE.get<PermissionCompliance>(
    '/api/admin/audit/permissions/compliance',
    { params: { staleDays } }
  );
  return data;
}

export type PermissionAuditExportFormat = 'csv' | 'json' | 'pdf';

export async function downloadPermissionAuditExport(
  format: PermissionAuditExportFormat,
  params: PermissionAuditReportParams = {}
): Promise<void> {
  const response = await AXIOS_INSTANCE.get('/api/admin/audit/permissions/export', {
    params: { ...params, format },
    responseType: 'blob',
  });

  const blob = response.data as Blob;
  const disposition = response.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const fallback = `permission-audit.${format === 'pdf' ? 'pdf' : format === 'json' ? 'json' : 'csv'}`;
  const filename = match?.[1] ?? fallback;

  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export type SchedulePermissionAuditReportRequest = {
  name: string;
  preset?: 'weekly' | 'monthly' | 'compliance';
  schedule?: string;
  recipients: string[];
  format: 'permission-csv' | 'permission-json' | 'permission-pdf' | 'csv' | 'json' | 'pdf';
  roleName?: string;
  actorUserId?: string;
  fromDate?: string;
  toDate?: string;
};

export async function schedulePermissionAuditReport(
  body: SchedulePermissionAuditReportRequest
): Promise<unknown> {
  const { data } = await AXIOS_INSTANCE.post('/api/admin/audit/permissions/schedule-report', body);
  return data;
}
