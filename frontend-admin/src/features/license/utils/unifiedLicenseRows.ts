import type { IssuedLicenseListItemDto } from '@/api/manual/adminLicense';
import type { LicenseSaleResponse } from '@/api/generated/model';

export type UnifiedLicenseKind = 'server' | 'tenant';

export type UnifiedLicenseRow = {
  id: string;
  kind: UnifiedLicenseKind;
  licenseKey: string;
  displayName: string;
  validUntilUtc: string | null;
  status: string;
  tenantSlug?: string | null;
};

export function mapIssuedLicenseToUnifiedRow(item: IssuedLicenseListItemDto): UnifiedLicenseRow {
  const status = item.isRevoked ? 'revoked' : item.isCancelled ? 'cancelled' : 'active';
  return {
    id: `server:${item.id}`,
    kind: 'server',
    licenseKey: item.licenseKey,
    displayName: item.customerName?.trim() || item.licenseKey,
    validUntilUtc: item.expiryAtUtc ?? null,
    status,
  };
}

export function mapLicenseSaleToUnifiedRow(sale: LicenseSaleResponse): UnifiedLicenseRow | null {
  const id = sale.id?.trim();
  const licenseKey = sale.licenseKey?.trim();
  if (!id || !licenseKey) return null;
  return {
    id: `tenant:${id}`,
    kind: 'tenant',
    licenseKey,
    displayName: sale.tenantName?.trim() || sale.tenantSlug?.trim() || licenseKey,
    validUntilUtc: sale.validUntilUtc ?? null,
    status: sale.status?.trim() || 'unknown',
    tenantSlug: sale.tenantSlug ?? null,
  };
}

export function mergeUnifiedLicenseRows(args: {
  issued: readonly IssuedLicenseListItemDto[];
  sales: readonly LicenseSaleResponse[];
  kindFilter?: UnifiedLicenseKind | 'all';
  search?: string;
}): UnifiedLicenseRow[] {
  const issuedRows = args.issued.map(mapIssuedLicenseToUnifiedRow);
  const saleRows = args.sales
    .map(mapLicenseSaleToUnifiedRow)
    .filter((row): row is UnifiedLicenseRow => row != null);

  let rows = [...issuedRows, ...saleRows];
  if (args.kindFilter && args.kindFilter !== 'all') {
    rows = rows.filter((row) => row.kind === args.kindFilter);
  }

  const needle = args.search?.trim().toLowerCase();
  if (needle) {
    rows = rows.filter((row) => {
      const blob = `${row.licenseKey} ${row.displayName} ${row.tenantSlug ?? ''} ${row.status}`;
      return blob.toLowerCase().includes(needle);
    });
  }

  return rows.sort((a, b) => {
    const aTime = a.validUntilUtc ? Date.parse(a.validUntilUtc) : 0;
    const bTime = b.validUntilUtc ? Date.parse(b.validUntilUtc) : 0;
    return bTime - aTime;
  });
}
