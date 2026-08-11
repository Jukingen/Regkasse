import type { LicenseSaleResponse } from '@/api/generated/model';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';

export type LicenseSalesCsvLabels = {
  invoiceNumber: string;
  tenantName: string;
  tenantSlug: string;
  licenseKey: string;
  licensePlan: string;
  licenseType: string;
  status: string;
  validFrom: string;
  validUntil: string;
  priceNet: string;
  priceGross: string;
  currency: string;
  soldAt: string;
};

const DEFAULT_LABELS: LicenseSalesCsvLabels = {
  invoiceNumber: 'Invoice number',
  tenantName: 'Tenant',
  tenantSlug: 'Slug',
  licenseKey: 'License key',
  licensePlan: 'Plan',
  licenseType: 'License type',
  status: 'Status',
  validFrom: 'Valid from',
  validUntil: 'Valid until',
  priceNet: 'Net',
  priceGross: 'Gross',
  currency: 'Currency',
  soldAt: 'Sold at',
};

export function buildLicenseSalesCsv(
  sales: LicenseSaleResponse[],
  labels: Partial<LicenseSalesCsvLabels> = {}
): string {
  const L = { ...DEFAULT_LABELS, ...labels };
  const header = [
    L.invoiceNumber,
    L.tenantName,
    L.tenantSlug,
    L.licenseKey,
    L.licensePlan,
    L.licenseType,
    L.status,
    L.validFrom,
    L.validUntil,
    L.priceNet,
    L.priceGross,
    L.currency,
    L.soldAt,
  ];

  const rows = sales.map((sale) => [
    sale.invoiceNumber ?? '',
    sale.tenantName ?? '',
    sale.tenantSlug ?? '',
    sale.licenseKey ?? '',
    sale.licensePlan ?? '',
    sale.licenseType ?? '',
    sale.status ?? '',
    sale.validFromUtc ?? '',
    sale.validUntilUtc ?? '',
    sale.priceNet ?? '',
    sale.priceGross ?? '',
    sale.currency ?? 'EUR',
    sale.soldAtUtc ?? '',
  ]);

  return rowsToCsv([header, ...rows]);
}

export function exportLicenseSalesCsv(
  sales: LicenseSaleResponse[],
  labels?: Partial<LicenseSalesCsvLabels>,
  fileName?: string
): void {
  const csv = buildLicenseSalesCsv(sales, labels);
  const stamp = new Date().toISOString().slice(0, 10);
  downloadCsvText(csv, fileName ?? `license-sales_${stamp}.csv`);
}
