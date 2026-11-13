import type { LicenseSaleResponse } from '@/api/generated/model';
import { isSaleCancellable } from '@/features/billing/utils/billingFormatters';

export const LICENSE_SALES_BULK_EXTEND_DAYS = {
  days30: 30,
  days90: 90,
  year1: 365,
} as const;

export type LicenseSalesBulkExtendDays =
  (typeof LICENSE_SALES_BULK_EXTEND_DAYS)[keyof typeof LICENSE_SALES_BULK_EXTEND_DAYS];

export type LicenseSalesBulkActionKind =
  | 'extend30'
  | 'extend90'
  | 'extend365'
  | 'revoke'
  | 'exportCsv';

export type LicenseSalesBulkExtendTarget = {
  tenantId: string;
  sale: LicenseSaleResponse;
  baseValidUntilUtc: string;
  nextValidUntilUtc: string;
};

/** Base for extension: later of now and current valid-until. */
export function computeLicenseExtendBaseUtc(
  validUntilUtc: string | null | undefined,
  now: Date = new Date()
): Date {
  const current = validUntilUtc ? new Date(validUntilUtc) : null;
  if (current && !Number.isNaN(current.getTime()) && current.getTime() > now.getTime()) {
    return current;
  }
  return now;
}

export function computeExtendedValidUntilUtc(
  validUntilUtc: string | null | undefined,
  addDays: number,
  now: Date = new Date()
): string {
  const base = computeLicenseExtendBaseUtc(validUntilUtc, now);
  const next = new Date(base.getTime());
  next.setUTCDate(next.getUTCDate() + addDays);
  return next.toISOString();
}

export function extendDaysForBulkAction(kind: LicenseSalesBulkActionKind): number | null {
  switch (kind) {
    case 'extend30':
      return LICENSE_SALES_BULK_EXTEND_DAYS.days30;
    case 'extend90':
      return LICENSE_SALES_BULK_EXTEND_DAYS.days90;
    case 'extend365':
      return LICENSE_SALES_BULK_EXTEND_DAYS.year1;
    default:
      return null;
  }
}

/**
 * One extend target per tenant: keep the sale with the latest validUntil among the selection.
 */
export function buildBulkExtendTargets(
  sales: LicenseSaleResponse[],
  addDays: number,
  now: Date = new Date()
): LicenseSalesBulkExtendTarget[] {
  const byTenant = new Map<string, LicenseSaleResponse>();

  for (const sale of sales) {
    const tenantId = sale.tenantId?.trim();
    if (!tenantId) continue;
    if ((sale.status ?? '').toLowerCase() === 'cancelled') continue;

    const existing = byTenant.get(tenantId);
    if (!existing) {
      byTenant.set(tenantId, sale);
      continue;
    }

    const existingUntil = existing.validUntilUtc
      ? new Date(existing.validUntilUtc).getTime()
      : Number.NEGATIVE_INFINITY;
    const nextUntil = sale.validUntilUtc
      ? new Date(sale.validUntilUtc).getTime()
      : Number.NEGATIVE_INFINITY;
    if (nextUntil >= existingUntil) {
      byTenant.set(tenantId, sale);
    }
  }

  return [...byTenant.entries()].map(([tenantId, sale]) => {
    const baseValidUntilUtc = computeLicenseExtendBaseUtc(sale.validUntilUtc, now).toISOString();
    return {
      tenantId,
      sale,
      baseValidUntilUtc,
      nextValidUntilUtc: computeExtendedValidUntilUtc(sale.validUntilUtc, addDays, now),
    };
  });
}

export function filterBulkRevokeSales(sales: LicenseSaleResponse[]): LicenseSaleResponse[] {
  return sales.filter((sale) => !!sale.id && isSaleCancellable(sale));
}

export type BulkItemResult = {
  id: string;
  label: string;
  ok: boolean;
  errorMessage?: string;
};

export type BulkRunProgress = {
  current: number;
  total: number;
  label: string;
};

export type BulkRunResult = {
  success: number;
  failed: number;
  total: number;
  results: BulkItemResult[];
};

export async function runBulkSequential<T>(
  items: T[],
  getMeta: (item: T) => { id: string; label: string },
  runOne: (item: T) => Promise<void>,
  onProgress?: (progress: BulkRunProgress) => void
): Promise<BulkRunResult> {
  const results: BulkItemResult[] = [];
  let success = 0;
  let failed = 0;

  for (let i = 0; i < items.length; i += 1) {
    const item = items[i]!;
    const meta = getMeta(item);
    onProgress?.({ current: i + 1, total: items.length, label: meta.label });
    try {
      await runOne(item);
      success += 1;
      results.push({ id: meta.id, label: meta.label, ok: true });
    } catch (err) {
      failed += 1;
      const errorMessage = err instanceof Error ? err.message : String(err);
      results.push({ id: meta.id, label: meta.label, ok: false, errorMessage });
    }
  }

  return { success, failed, total: items.length, results };
}
