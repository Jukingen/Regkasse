import type { AuditLogListParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import { buildAuditLogExportQuery } from '@/features/audit-logs/utils/buildAuditLogExportQuery';
import { buildAuditExportFileName } from '@/features/audit/utils/auditExportFileName';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type AuditExportFormat = 'csv' | 'json' | 'excel';

export type AuditRetentionInfo = {
  retentionYears: number;
  minCutoffDate: string;
  message: string;
};

export type AuditExportJobStatus = {
  jobId: string;
  status: 'Queued' | 'Running' | 'Completed' | 'Failed' | number;
  matchedRows?: number;
  message?: string;
  downloadFileName?: string;
};

export type AuditReportSchedule = {
  id: string;
  name: string;
  scheduleCron: string;
  format: string;
  isActive: boolean;
  recipients: string[];
  lastRunUtc?: string;
  nextRunUtc?: string;
  createdAtUtc: string;
};

function toFiltersBody(params: AuditLogListParams) {
  const q = buildAuditLogExportQuery(params);
  return {
    startDate: q.startDate,
    endDate: q.endDate,
    userId: q.userId,
    targetUserId: q.targetUserId,
    action: q.action,
    entityType: q.entityType,
    entityId: q.entityId,
    ipAddress: q.ipAddress,
    status: q.status,
    statusOutcome: q.statusOutcome,
    hasChanges: params.hasChanges,
  };
}

export async function fetchAuditRetention(): Promise<AuditRetentionInfo> {
  const res = await AXIOS_INSTANCE.get<AuditRetentionInfo>('/api/admin/audit/retention');
  return res.data;
}

export async function startAuditExport(
  format: AuditExportFormat,
  params: AuditLogListParams,
  securityHeaders?: Record<string, string>
): Promise<
  | { kind: 'immediate'; blob: Blob; fileName: string }
  | { kind: 'background'; jobId: string; matchedRows?: number }
> {
  const res = await AXIOS_INSTANCE.post(
    '/api/admin/audit/export',
    { format, filters: toFiltersBody(params) },
    {
      responseType: 'blob',
      validateStatus: (s) => s === 200 || s === 202,
      headers: securityHeaders,
    }
  );

  if (res.status === 202) {
    const text = await (res.data as Blob).text();
    const parsed = JSON.parse(text) as { jobId: string; matchedRows?: number };
    return { kind: 'background', jobId: parsed.jobId, matchedRows: parsed.matchedRows };
  }

  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const fileName =
    match?.[1] ??
    buildAuditExportFileName({
      tenantSlug: getEffectiveTenantSlug(),
      fromDate: params.startDate,
      toDate: params.endDate,
      format,
    });
  return { kind: 'immediate', blob: res.data as Blob, fileName };
}

export async function pollAuditExportJob(jobId: string): Promise<AuditExportJobStatus> {
  const res = await AXIOS_INSTANCE.get<AuditExportJobStatus>(
    `/api/admin/audit/export/jobs/${jobId}`
  );
  return res.data;
}

export async function downloadAuditExportJob(
  jobId: string,
  fileName: string,
  securityHeaders?: Record<string, string>
): Promise<void> {
  const res = await AXIOS_INSTANCE.get<Blob>(`/api/admin/audit/export/jobs/${jobId}/download`, {
    responseType: 'blob',
    headers: securityHeaders,
  });
  const url = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

export async function scheduleAuditReport(input: {
  name: string;
  schedule: string;
  recipients: string[];
  format: AuditExportFormat;
  params: AuditLogListParams;
}): Promise<AuditReportSchedule> {
  const res = await AXIOS_INSTANCE.post<AuditReportSchedule>('/api/admin/audit/schedule-report', {
    name: input.name,
    schedule: input.schedule,
    recipients: input.recipients,
    format: input.format,
    filters: toFiltersBody(input.params),
  });
  return res.data;
}

export async function listAuditReportSchedules(): Promise<AuditReportSchedule[]> {
  const res = await AXIOS_INSTANCE.get<AuditReportSchedule[]>('/api/admin/audit/schedules');
  return res.data;
}

export async function deactivateAuditReportSchedule(id: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/audit/schedules/${id}`);
}
