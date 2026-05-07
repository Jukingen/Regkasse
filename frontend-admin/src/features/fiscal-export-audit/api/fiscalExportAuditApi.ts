import { AXIOS_INSTANCE } from '@/lib/axios';

export type FiscalExportAuditExportTypeQuery = 'all' | 'pdf' | 'json' | 'csv';

export interface FiscalExportAuditLogListItem {
    id: string;
    downloadTimeUtc: string;
    userId: string;
    username: string;
    ipAddress: string | null;
    exportTypeLabel: string;
    includesCsvFragment: boolean;
    exportPeriodFromUtc: string | null;
    exportPeriodToUtc: string | null;
    estimatedFileSizeBytes: number | null;
    success: boolean;
    longRangeBulkWarning: boolean;
}

export interface FiscalExportAuditLogDetail extends FiscalExportAuditLogListItem {
    action: string;
    description: string | null;
    userRole: string;
    requestDataJson: string | null;
    responseDataJson: string | null;
    errorDetails: string | null;
}

export interface FiscalExportAuditLogsPagedResponse {
    items: FiscalExportAuditLogListItem[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}

export interface FiscalExportAuditListParams {
    downloadFrom?: string;
    downloadTo?: string;
    userSearch?: string;
    exportType?: FiscalExportAuditExportTypeQuery;
    page?: number;
    pageSize?: number;
}

export async function fetchFiscalExportAuditLogs(
    params: FiscalExportAuditListParams,
): Promise<FiscalExportAuditLogsPagedResponse> {
    const { data } = await AXIOS_INSTANCE.get<FiscalExportAuditLogsPagedResponse>('/api/admin/audit/fiscal-export-logs', {
        params: {
            downloadFrom: params.downloadFrom,
            downloadTo: params.downloadTo,
            userSearch: params.userSearch,
            exportType: params.exportType ?? 'all',
            page: params.page ?? 1,
            pageSize: params.pageSize ?? 25,
        },
    });
    return data;
}

export async function fetchFiscalExportAuditDetail(id: string): Promise<FiscalExportAuditLogDetail> {
    const { data } = await AXIOS_INSTANCE.get<FiscalExportAuditLogDetail>(`/api/admin/audit/fiscal-export-logs/${id}`);
    return data;
}

export function buildFiscalExportAuditCsvExportUrl(params: FiscalExportAuditListParams & { maxRows?: number }): string {
    const q = new URLSearchParams();
    if (params.downloadFrom) q.set('downloadFrom', params.downloadFrom);
    if (params.downloadTo) q.set('downloadTo', params.downloadTo);
    if (params.userSearch) q.set('userSearch', params.userSearch);
    q.set('exportType', params.exportType ?? 'all');
    q.set('maxRows', String(params.maxRows ?? 5000));
    return `/api/admin/audit/fiscal-export-logs/export?${q.toString()}`;
}
