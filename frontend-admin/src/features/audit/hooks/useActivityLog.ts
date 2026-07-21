'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';

import type { AuditLogEntryDto } from '@/api/generated/model';
import { isPlatformUserRole } from '@/features/users/utils/userScope';
import { customInstance } from '@/lib/axios';

export type ActivityLogFilters = {
  dateRange: [Dayjs | null, Dayjs | null] | null;
  userId?: string;
  actionType: string;
  search: string;
  page: number;
  pageSize: number;
};

export type ActivityLogRow = {
  id: string;
  timestamp: string;
  userName: string;
  action: string;
  description: string;
  details: Record<string, unknown> | string | null;
};

type AuditLogsListResponse = {
  auditLogs?: AuditLogEntryDto[] | null;
  totalCount?: number;
};

const DEFAULT_PAGE_SIZE = 20;

function mapAuditRow(row: AuditLogEntryDto): ActivityLogRow {
  let details: Record<string, unknown> | string | null = null;
  if (row.metadata?.trim()) {
    try {
      details = JSON.parse(row.metadata) as Record<string, unknown>;
    } catch {
      details = row.metadata;
    }
  } else if (row.changes?.trim() || row.newValues?.trim() || row.oldValues?.trim()) {
    details = {
      changes: row.changes ?? undefined,
      newValues: row.newValues ?? undefined,
      oldValues: row.oldValues ?? undefined,
    };
  }

  return {
    id: row.id ?? '',
    timestamp: row.timestamp ?? row.createdAt ?? '',
    userName: row.actorDisplayName?.trim() || row.userId?.trim() || '—',
    action: row.action?.trim() || '—',
    description: row.description?.trim() || '—',
    details,
  };
}

function buildQueryParams(
  filters: ActivityLogFilters
): Record<string, string | number | boolean | undefined> {
  const [from, to] = filters.dateRange ?? [null, null];
  const params: Record<string, string | number | boolean | undefined> = {
    page: filters.page,
    pageSize: filters.pageSize,
    includeTotalCount: true,
    userId: filters.userId,
    action: filters.actionType || undefined,
    startDate: from ? from.startOf('day').toISOString() : undefined,
    endDate: to ? to.endOf('day').toISOString() : undefined,
  };
  const search = filters.search.trim();
  if (search) {
    params.search = search;
  }
  return params;
}

async function fetchActivityAuditLogs(
  filters: ActivityLogFilters,
  signal?: AbortSignal
): Promise<AuditLogsListResponse> {
  return customInstance<AuditLogsListResponse>({
    url: '/api/AuditLog',
    method: 'GET',
    params: buildQueryParams(filters),
    signal,
  });
}

/**
 * Tenant-scoped audit activity list for Manager oversight.
 * Uses GET /api/AuditLog (EF tenant filter + backend hides platform operator rows).
 */
export function useActivityLog(filters: ActivityLogFilters) {
  const query = useQuery({
    queryKey: ['activity-log', filters],
    queryFn: ({ signal }) => fetchActivityAuditLogs(filters, signal),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

  const rows = (query.data?.auditLogs ?? [])
    .filter((row) => !isPlatformUserRole(row.userRole))
    .map(mapAuditRow);

  return {
    activities: rows,
    total: query.data?.totalCount ?? rows.length,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
    isPlaceholderData: query.isPlaceholderData,
    pageSize: filters.pageSize || DEFAULT_PAGE_SIZE,
  };
}

export function defaultActivityLogFilters(): ActivityLogFilters {
  return {
    dateRange: [dayjs().subtract(7, 'day'), dayjs()],
    userId: undefined,
    actionType: '',
    search: '',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  };
}
