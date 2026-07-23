import { customInstance } from '@/lib/axios';

export type PermissionAnalyticsNamedCountDto = {
  key: string;
  label: string;
  userCount: number;
  percent: number;
};

export type PermissionAnalyticsRecommendationDto = {
  code: string;
  severity: string;
  message: string;
  arg?: string | null;
};

export type PermissionAnalyticsSummaryDto = {
  totalUsers: number;
  totalRoles: number;
  totalPermissions: number;
  mostUsed: PermissionAnalyticsNamedCountDto[];
  leastUsed: PermissionAnalyticsNamedCountDto[];
  roleDistribution: PermissionAnalyticsNamedCountDto[];
  overPrivilegedUsers: PermissionAnalyticsNamedCountDto[];
  unusedPermissions: string[];
  recommendations: PermissionAnalyticsRecommendationDto[];
};

export type PermissionAnalyticsTrendPointDto = {
  date: string;
  totalUsers: number;
  payloadJson: string;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

function asStringList(value: unknown): string[] {
  return Array.isArray(value) ? value.map((v) => String(v)).filter(Boolean) : [];
}

function mapNamed(raw: unknown): PermissionAnalyticsNamedCountDto {
  const row = asRecord(raw);
  return {
    key: String(row.key ?? row.Key ?? ''),
    label: String(row.label ?? row.Label ?? row.key ?? row.Key ?? ''),
    userCount: Number(row.userCount ?? row.UserCount ?? 0),
    percent: Number(row.percent ?? row.Percent ?? 0),
  };
}

function mapRecommendation(raw: unknown): PermissionAnalyticsRecommendationDto {
  const row = asRecord(raw);
  return {
    code: String(row.code ?? row.Code ?? ''),
    severity: String(row.severity ?? row.Severity ?? 'info'),
    message: String(row.message ?? row.Message ?? ''),
    arg: (row.arg ?? row.Arg ?? null) as string | null,
  };
}

export async function fetchPermissionAnalyticsSummary(): Promise<PermissionAnalyticsSummaryDto> {
  const res = await customInstance<Record<string, unknown>>({
    url: '/api/admin/permission-analytics/summary',
    method: 'GET',
  });
  return {
    totalUsers: Number(res.totalUsers ?? res.TotalUsers ?? 0),
    totalRoles: Number(res.totalRoles ?? res.TotalRoles ?? 0),
    totalPermissions: Number(res.totalPermissions ?? res.TotalPermissions ?? 0),
    mostUsed: (Array.isArray(res.mostUsed ?? res.MostUsed) ? (res.mostUsed ?? res.MostUsed) : []).map(
      mapNamed
    ),
    leastUsed: (Array.isArray(res.leastUsed ?? res.LeastUsed)
      ? (res.leastUsed ?? res.LeastUsed)
      : []
    ).map(mapNamed),
    roleDistribution: (Array.isArray(res.roleDistribution ?? res.RoleDistribution)
      ? (res.roleDistribution ?? res.RoleDistribution)
      : []
    ).map(mapNamed),
    overPrivilegedUsers: (Array.isArray(res.overPrivilegedUsers ?? res.OverPrivilegedUsers)
      ? (res.overPrivilegedUsers ?? res.OverPrivilegedUsers)
      : []
    ).map(mapNamed),
    unusedPermissions: asStringList(res.unusedPermissions ?? res.UnusedPermissions),
    recommendations: (Array.isArray(res.recommendations ?? res.Recommendations)
      ? (res.recommendations ?? res.Recommendations)
      : []
    ).map(mapRecommendation),
  };
}

export async function fetchPermissionAnalyticsTrend(
  days = 30
): Promise<PermissionAnalyticsTrendPointDto[]> {
  const res = await customInstance<unknown[]>({
    url: '/api/admin/permission-analytics/trend',
    method: 'GET',
    params: { days },
  });
  return (Array.isArray(res) ? res : []).map((raw) => {
    const row = asRecord(raw);
    return {
      date: String(row.date ?? row.Date ?? ''),
      totalUsers: Number(row.totalUsers ?? row.TotalUsers ?? 0),
      payloadJson: String(row.payloadJson ?? row.PayloadJson ?? '{}'),
    };
  });
}

/** Downloads analytics PDF when backend export is available. */
export async function exportPermissionAnalyticsPdf(): Promise<Blob> {
  return customInstance<Blob>({
    url: '/api/admin/permission-analytics/export',
    method: 'GET',
    params: { format: 'pdf' },
    responseType: 'blob',
  });
}
