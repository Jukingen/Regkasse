'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';

import {
  getPermissionAuditLogs,
  type AuditParams,
  type PaginatedResponse,
  type PermissionAuditEntry,
} from '@/features/audit/api/permissionAudit';

export type { AuditParams, PermissionAuditEntry, PaginatedResponse };

export const permissionAuditQueryKey = (params: AuditParams) =>
  ['permission-audit', params] as const;

/**
 * Paginated permission / role change history from GET /api/admin/audit/permissions.
 */
export function usePermissionAudit(
  params: AuditParams,
  options?: { enabled?: boolean }
) {
  return useQuery({
    queryKey: permissionAuditQueryKey(params),
    queryFn: () => getPermissionAuditLogs(params),
    enabled: options?.enabled !== false,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });
}
