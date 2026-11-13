import type { IssuedLicenseListItemDto } from '@/api/manual/adminLicense';
import type { LicenseSaleResponse } from '@/api/generated/model';
import {
  TENANT_EXPIRING_SOON_DAYS,
  TENANT_GRACE_PERIOD_DAYS,
} from '@/features/license/constants/licenseGracePeriod';

export type UnifiedLicenseKind = 'system' | 'tenant';

export type UnifiedLicenseRowStatus =
  | 'active'
  | 'expiringSoon'
  | 'grace'
  | 'expired'
  | 'locked';

export type UnifiedLicenseRow = {
  id: string;
  kind: UnifiedLicenseKind;
  licenseKey: string;
  displayName: string;
  validUntilUtc: string | null;
  status: UnifiedLicenseRowStatus;
  slug: string | null;
  tenantSlug?: string | null;
  tenantId?: string | null;
};

const DAY_MS = 24 * 60 * 60 * 1000;

/** Parses `REGK-{yyyyMMdd}-{slug}-{8}` and returns the slug, or null. */
export function parseUnifiedLicenseSlug(licenseKey: string): string | null {
  const parts = licenseKey.trim().split('-');
  if (parts.length < 4 || parts[0]?.toUpperCase() !== 'REGK') {
    return null;
  }
  const slug = parts.slice(2, -1).join('-').toLowerCase();
  return slug.length > 0 ? slug : null;
}

export type LicenseUnlockTarget = 'system' | 'tenant' | 'unknown';

/** Detects whether a REGK key unlocks the deployment (system) or the mandant. */
export function resolveLicenseUnlockTarget(licenseKey: string): LicenseUnlockTarget {
  const slug = parseUnifiedLicenseSlug(licenseKey);
  if (slug === 'system') return 'system';
  if (slug) return 'tenant';
  return 'unknown';
}

/** True when the key slug is a mandant slug that does not match the current/selected tenant. */
export function isLicenseSlugMismatch(
  licenseKey: string,
  currentTenantSlug: string | null | undefined
): boolean {
  const slug = parseUnifiedLicenseSlug(licenseKey);
  if (!slug || slug === 'system') return false;
  const current = currentTenantSlug?.trim().toLowerCase();
  if (!current) return false;
  return slug !== current;
}

export function resolveUnifiedLicenseKind(
  licenseKey: string,
  fallback: UnifiedLicenseKind
): UnifiedLicenseKind {
  const slug = parseUnifiedLicenseSlug(licenseKey);
  if (slug === 'system') return 'system';
  if (slug) return 'tenant';
  return fallback;
}

export function resolveUnifiedLicenseRowStatus(args: {
  validUntilUtc: string | null;
  isRevoked?: boolean;
  isCancelled?: boolean;
  saleStatus?: string | null;
  nowMs?: number;
}): UnifiedLicenseRowStatus {
  const sale = args.saleStatus?.trim().toLowerCase();
  if (args.isRevoked || args.isCancelled || sale === 'cancelled' || sale === 'revoked') {
    return 'locked';
  }

  if (!args.validUntilUtc) {
    return 'expired';
  }

  const until = Date.parse(args.validUntilUtc);
  if (!Number.isFinite(until)) {
    return 'expired';
  }

  const now = args.nowMs ?? Date.now();
  if (until >= now) {
    const daysRemaining = Math.ceil((until - now) / DAY_MS);
    if (daysRemaining <= TENANT_EXPIRING_SOON_DAYS) {
      return 'expiringSoon';
    }
    return 'active';
  }

  const daysOverdue = Math.floor((now - until) / DAY_MS);
  if (daysOverdue <= TENANT_GRACE_PERIOD_DAYS) {
    return 'grace';
  }

  return 'expired';
}

export function mapIssuedLicenseToUnifiedRow(item: IssuedLicenseListItemDto): UnifiedLicenseRow {
  const slug = parseUnifiedLicenseSlug(item.licenseKey) ?? 'system';
  return {
    id: `system:${item.id}`,
    kind: resolveUnifiedLicenseKind(item.licenseKey, 'system'),
    licenseKey: item.licenseKey,
    displayName: item.customerName?.trim() || item.licenseKey,
    validUntilUtc: item.expiryAtUtc ?? null,
    status: resolveUnifiedLicenseRowStatus({
      validUntilUtc: item.expiryAtUtc ?? null,
      isRevoked: item.isRevoked,
      isCancelled: item.isCancelled,
    }),
    slug,
    tenantSlug: slug === 'system' ? null : slug,
    tenantId: null,
  };
}

export function mapLicenseSaleToUnifiedRow(sale: LicenseSaleResponse): UnifiedLicenseRow | null {
  const id = sale.id?.trim();
  const licenseKey = sale.licenseKey?.trim();
  if (!id || !licenseKey) return null;
  const slug = sale.tenantSlug?.trim() || parseUnifiedLicenseSlug(licenseKey);
  return {
    id: `tenant:${id}`,
    kind: resolveUnifiedLicenseKind(licenseKey, 'tenant'),
    licenseKey,
    displayName: sale.tenantName?.trim() || slug || licenseKey,
    validUntilUtc: sale.validUntilUtc ?? null,
    status: resolveUnifiedLicenseRowStatus({
      validUntilUtc: sale.validUntilUtc ?? null,
      saleStatus: sale.status,
    }),
    slug: slug ?? null,
    tenantSlug: slug ?? null,
    tenantId: sale.tenantId?.trim() || null,
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

  const seenKeys = new Set<string>();
  let rows: UnifiedLicenseRow[] = [];
  for (const row of [...issuedRows, ...saleRows]) {
    const key = row.licenseKey.trim().toUpperCase();
    if (seenKeys.has(key)) continue;
    seenKeys.add(key);
    rows.push(row);
  }

  if (args.kindFilter && args.kindFilter !== 'all') {
    rows = rows.filter((row) => row.kind === args.kindFilter);
  }

  const needle = args.search?.trim().toLowerCase();
  if (needle) {
    rows = rows.filter((row) => {
      const blob = `${row.licenseKey} ${row.displayName} ${row.slug ?? ''} ${row.status}`;
      return blob.toLowerCase().includes(needle);
    });
  }

  return rows.sort((a, b) => {
    const aTime = a.validUntilUtc ? Date.parse(a.validUntilUtc) : 0;
    const bTime = b.validUntilUtc ? Date.parse(b.validUntilUtc) : 0;
    return bTime - aTime;
  });
}
