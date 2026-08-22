import type { LimitDashboardDto, LimitStatusDto } from '@/features/tenants/api/tenantLimits';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';

export type LimitDashboardCsvLabels = {
  tenant: string;
  key: string;
  name: string;
  current: string;
  limit: string;
  percentage: string;
  status: string;
  trend: string;
  changeCount: string;
  changeUnit: string;
};

export function buildLimitDashboardCsvRows(
  limits: readonly LimitStatusDto[],
  labels: LimitDashboardCsvLabels,
  nameOf: (key: string, fallback: string) => string
): string {
  const header = [
    labels.tenant,
    labels.key,
    labels.name,
    labels.current,
    labels.limit,
    labels.percentage,
    labels.status,
    labels.trend,
    labels.changeCount,
    labels.changeUnit,
  ];
  const body = limits.map((row) => [
    row.tenantName || row.tenantId,
    row.key,
    nameOf(row.key, row.displayName),
    row.current,
    row.limit,
    row.percentage,
    row.status,
    row.trend,
    row.changeCount,
    row.changeUnit,
  ]);
  return rowsToCsv([header, ...body]);
}

export function exportLimitDashboardCsv(
  data: Pick<LimitDashboardDto, 'limits'>,
  labels: LimitDashboardCsvLabels,
  nameOf: (key: string, fallback: string) => string,
  fileName?: string
): void {
  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '');
  downloadCsvText(
    buildLimitDashboardCsvRows(data.limits, labels, nameOf),
    fileName ?? `limit-dashboard_${stamp}.csv`
  );
}
