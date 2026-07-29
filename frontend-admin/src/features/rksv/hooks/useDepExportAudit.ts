'use client';

import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DepExportAuditAction =
  | 'Created'
  | 'Downloaded'
  | 'Archived'
  | 'Deleted'
  | 'Validated'
  | 'Failed'
  | string;

export type DepExportAuditEntryDto = {
  id: string;
  tenantId: string;
  action: DepExportAuditAction;
  exportName: string;
  exportHistoryId?: string | null;
  userEmail?: string | null;
  userId?: string | null;
  userRole?: string | null;
  actionAt: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  details?: string | null;
};

export type DepExportAuditReportDto = {
  tenantId: string;
  generatedAtUtc: string;
  fromUtc: string;
  toUtc: string;
  totalEntries: number;
  countsByAction: Record<string, number>;
  lastActionAt?: string | null;
  lastAction?: string | null;
  lastExportName?: string | null;
  recentEntries: DepExportAuditEntryDto[];
  disclaimer?: string;
};

export type DepExportAuditTrailParams = {
  fromUtc?: string;
  toUtc?: string;
  action?: string;
  userSearch?: string;
  limit?: number;
};

export const depExportAuditQueryKeys = {
  all: ['rksv', 'dep-export-audit'] as const,
  trail: (params: DepExportAuditTrailParams) =>
    ['rksv', 'dep-export-audit', 'trail', params] as const,
  report: (fromUtc?: string, toUtc?: string) =>
    ['rksv', 'dep-export-audit', 'report', fromUtc ?? 'default', toUtc ?? 'default'] as const,
};

export async function fetchDepExportAuditTrail(
  params: DepExportAuditTrailParams = {}
): Promise<DepExportAuditEntryDto[]> {
  const response = await AXIOS_INSTANCE.get<DepExportAuditEntryDto[]>(
    '/api/admin/rksv/dep-export/audit-trail',
    { params }
  );
  return response.data ?? [];
}

export async function fetchDepExportAuditReport(
  fromUtc?: string,
  toUtc?: string
): Promise<DepExportAuditReportDto> {
  const response = await AXIOS_INSTANCE.get<DepExportAuditReportDto>(
    '/api/admin/rksv/dep-export/audit-report',
    { params: { fromUtc, toUtc } }
  );
  return {
    ...response.data,
    countsByAction: response.data.countsByAction ?? {},
    recentEntries: response.data.recentEntries ?? [],
  };
}

export function useDepExportAuditTrail(params: DepExportAuditTrailParams) {
  return useQuery({
    queryKey: depExportAuditQueryKeys.trail(params),
    queryFn: () => fetchDepExportAuditTrail(params),
    staleTime: 30_000,
  });
}

export function useDepExportAuditReport(fromUtc?: string, toUtc?: string) {
  return useQuery({
    queryKey: depExportAuditQueryKeys.report(fromUtc, toUtc),
    queryFn: () => fetchDepExportAuditReport(fromUtc, toUtc),
    staleTime: 60_000,
  });
}

export function auditActionTagColor(action: string): string {
  switch (action) {
    case 'Created':
      return 'processing';
    case 'Downloaded':
      return 'default';
    case 'Archived':
      return 'success';
    case 'Deleted':
      return 'error';
    case 'Validated':
      return 'cyan';
    case 'Failed':
      return 'warning';
    default:
      return 'default';
  }
}
