import dayjs from 'dayjs';
import { toAuditLogStatusApiParam } from '@/features/audit-logs/constants/auditLogFilters';
import type { AuditLogListParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';

export function buildAuditLogExportQuery(params: AuditLogListParams): Record<string, string> {
    const exportParams: Record<string, string> = {};
    if (params.startDate) {
        exportParams.startDate = dayjs(params.startDate).startOf('day').toISOString();
    }
    if (params.endDate) {
        exportParams.endDate = dayjs(params.endDate).endOf('day').toISOString();
    }
    if (params.action) exportParams.action = params.action;
    if (params.userId) exportParams.userId = params.userId;
    if (params.entityType) exportParams.entityType = params.entityType;
    const status = toAuditLogStatusApiParam(params.status);
    if (status) exportParams.status = status;
    return exportParams;
}
