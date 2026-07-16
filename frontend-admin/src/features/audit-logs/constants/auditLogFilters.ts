/** Backend `AuditLogEntityTypes` — see `KasseAPI_Final.Models.AuditLog.AuditLogEntityTypes`. */
export const AUDIT_LOG_ENTITY_TYPE_FILTER_VALUES = [
    'Tenant',
    'Payment',
    'Invoice',
    'Receipt',
    'Cart',
    'CartItem',
    'Customer',
    'User',
    'Role',
    'PaymentSession',
    'PaymentLog',
    'AuditLog',
    'SystemConfig',
    'TseDevice',
    'PosCritical',
    'DailyClosing',
  'FiscalExport',
] as const;

export type AuditLogEntityTypeFilter = (typeof AUDIT_LOG_ENTITY_TYPE_FILTER_VALUES)[number];

import type { AuditLogStatus } from '@/api/generated/model/auditLogStatus';
import { AuditLogStatus as AuditLogStatusEnum } from '@/api/generated/model/auditLogStatus';

/** Operator-facing status filter values (API enum names). */
export const AUDIT_LOG_STATUS_FILTER_VALUES = ['Success', 'Failed', 'Warning'] as const;

export type AuditLogStatusFilter = (typeof AUDIT_LOG_STATUS_FILTER_VALUES)[number];

/** Shareable URL uses `Failure`; ASP.NET `AuditLogStatus` enum binds `Failed`. */
export function parseAuditLogStatusFromUrl(raw: string | undefined): AuditLogStatusFilter | undefined {
    const trimmed = raw?.trim();
    if (!trimmed) return undefined;
    if (trimmed === 'Failure') return 'Failed';
    if ((AUDIT_LOG_STATUS_FILTER_VALUES as readonly string[]).includes(trimmed)) {
        return trimmed as AuditLogStatusFilter;
    }
    return undefined;
}

export function toAuditLogStatusUrlParam(status: AuditLogStatusFilter): string {
    return status === 'Failed' ? 'Failure' : status;
}

const AUDIT_LOG_STATUS_FILTER_TO_API: Record<AuditLogStatusFilter, AuditLogStatus> = {
    Success: AuditLogStatusEnum.NUMBER_0,
    Failed: AuditLogStatusEnum.NUMBER_1,
    Warning: AuditLogStatusEnum.NUMBER_9,
};

export function toAuditLogStatusApiParam(status: AuditLogStatusFilter | undefined): AuditLogStatus | undefined {
    return status ? AUDIT_LOG_STATUS_FILTER_TO_API[status] : undefined;
}

export { AUDIT_ACTION_FILTER_VALUES, type AuditActionFilter } from '@/features/audit-logs/utils/auditActionLabels';

export const AUDIT_LOG_LIST_DEFAULTS = {
    page: 1,
    pageSize: 10,
} as const;
