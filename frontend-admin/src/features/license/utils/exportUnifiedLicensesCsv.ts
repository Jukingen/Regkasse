import type { UnifiedLicenseRow } from '@/features/license/utils/unifiedLicenseRows';
import { maskTenantLicenseKey } from '@/features/license/utils/tenantLicenseExtend';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';

export type UnifiedLicenseCsvLabels = {
  kind: string;
  licenseKey: string;
  slug: string;
  displayName: string;
  validUntil: string;
  status: string;
  tenantId: string;
};

export function exportUnifiedLicensesCsv(
  rows: readonly UnifiedLicenseRow[],
  labels: UnifiedLicenseCsvLabels,
  options?: { fileName?: string; maskLicenseKeys?: boolean }
): void {
  const header = [
    labels.kind,
    labels.licenseKey,
    labels.slug,
    labels.displayName,
    labels.validUntil,
    labels.status,
    labels.tenantId,
  ];
  const body = rows.map((row) => [
    row.kind,
    options?.maskLicenseKeys ? maskTenantLicenseKey(row.licenseKey) : row.licenseKey,
    row.slug ?? '',
    row.displayName,
    row.validUntilUtc ?? '',
    row.status,
    row.tenantId ?? '',
  ]);
  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '');
  downloadCsvText(
    rowsToCsv([header, ...body]),
    options?.fileName ?? `licenses_${stamp}.csv`
  );
}
