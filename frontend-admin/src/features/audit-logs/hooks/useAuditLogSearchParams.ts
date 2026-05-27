'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { useCallback, useMemo } from 'react';
import {
    AUDIT_LOG_LIST_DEFAULTS,
    parseAuditLogStatusFromUrl,
    toAuditLogStatusUrlParam,
    type AuditLogEntityTypeFilter,
    type AuditLogStatusFilter,
} from '@/features/audit-logs/constants/auditLogFilters';

export type AuditLogListParams = {
    page: number;
    pageSize: number;
    startDate?: string;
    endDate?: string;
    action?: string;
    userId?: string;
    targetUserId?: string;
    entityType?: AuditLogEntityTypeFilter;
    entityId?: string;
    ipAddress?: string;
    status?: AuditLogStatusFilter;
    statusOutcome?: 'success' | 'failure';
    hasChanges?: boolean;
};

const FILTER_KEYS = [
    'startDate',
    'endDate',
    'action',
    'userId',
    'targetUserId',
    'entityType',
    'entityId',
    'ipAddress',
    'status',
    'statusOutcome',
    'hasChanges',
] as const;

function parsePositiveInt(raw: string | null, fallback: number): number {
    if (!raw) return fallback;
    const n = parseInt(raw, 10);
    return Number.isFinite(n) && n > 0 ? n : fallback;
}

/** Parse URL searchParams into typed audit-log list params with defaults. */
export function useAuditLogSearchParams() {
    const searchParams = useSearchParams();
    const router = useRouter();
    const pathname = usePathname();

    const params: AuditLogListParams = useMemo(() => {
        const raw = Object.fromEntries(searchParams.entries());
        return {
            page: parsePositiveInt(raw.page ?? null, AUDIT_LOG_LIST_DEFAULTS.page),
            pageSize: parsePositiveInt(raw.pageSize ?? null, AUDIT_LOG_LIST_DEFAULTS.pageSize),
            startDate: raw.startDate?.trim() || undefined,
            endDate: raw.endDate?.trim() || undefined,
            action: raw.action?.trim() || undefined,
            userId: raw.userId?.trim() || undefined,
            targetUserId: raw.targetUserId?.trim() || undefined,
            entityType: (raw.entityType?.trim() || undefined) as AuditLogEntityTypeFilter | undefined,
            entityId: raw.entityId?.trim() || undefined,
            ipAddress: raw.ipAddress?.trim() || undefined,
            status: parseAuditLogStatusFromUrl(raw.status),
            statusOutcome:
                raw.statusOutcome === 'success' || raw.statusOutcome === 'failure'
                    ? raw.statusOutcome
                    : undefined,
            hasChanges:
                raw.hasChanges === 'true' ? true : raw.hasChanges === 'false' ? false : undefined,
        };
    }, [searchParams]);

    const setParams = useCallback(
        (partial: Partial<AuditLogListParams>) => {
            const isFilterChange = Object.keys(partial).some(
                (k) => FILTER_KEYS.includes(k as (typeof FILTER_KEYS)[number]),
            );

            const merged: AuditLogListParams = {
                ...params,
                ...partial,
                page: isFilterChange ? 1 : (partial.page ?? params.page),
            };

            const qp = new URLSearchParams();
            if (merged.page !== AUDIT_LOG_LIST_DEFAULTS.page) qp.set('page', String(merged.page));
            if (merged.pageSize !== AUDIT_LOG_LIST_DEFAULTS.pageSize) qp.set('pageSize', String(merged.pageSize));
            if (merged.startDate) qp.set('startDate', merged.startDate);
            if (merged.endDate) qp.set('endDate', merged.endDate);
            if (merged.action) qp.set('action', merged.action);
            if (merged.userId) qp.set('userId', merged.userId);
            if (merged.targetUserId) qp.set('targetUserId', merged.targetUserId);
            if (merged.entityType) qp.set('entityType', merged.entityType);
            if (merged.entityId) qp.set('entityId', merged.entityId);
            if (merged.ipAddress) qp.set('ipAddress', merged.ipAddress);
            if (merged.status) qp.set('status', toAuditLogStatusUrlParam(merged.status));
            if (merged.statusOutcome) qp.set('statusOutcome', merged.statusOutcome);
            if (merged.hasChanges === true) qp.set('hasChanges', 'true');
            if (merged.hasChanges === false) qp.set('hasChanges', 'false');

            const qs = qp.toString();
            // App Router: scroll:false avoids full navigation; URL updates without reload.
            router.push(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
        },
        [params, pathname, router],
    );

    const resetFilters = useCallback(() => {
        router.push(pathname, { scroll: false });
    }, [pathname, router]);

    return { params, setParams, resetFilters };
}
