import { describe, expect, it, vi } from 'vitest';

import type { LicenseSaleResponse } from '@/api/generated/model';
import {
  buildBulkExtendTargets,
  computeExtendedValidUntilUtc,
  computeLicenseExtendBaseUtc,
  filterBulkRevokeSales,
  runBulkSequential,
} from '@/features/billing/utils/billingSalesBulk';
import { buildLicenseSalesCsv } from '@/features/billing/utils/exportLicenseSalesCsv';

function sale(partial: Partial<LicenseSaleResponse>): LicenseSaleResponse {
  return {
    id: partial.id ?? 'sale-1',
    tenantId: partial.tenantId ?? 'tenant-1',
    tenantName: partial.tenantName ?? 'Cafe',
    tenantSlug: partial.tenantSlug ?? 'cafe',
    status: partial.status ?? 'active',
    validUntilUtc: partial.validUntilUtc,
    invoiceNumber: partial.invoiceNumber ?? 'INV-1',
    licenseKey: partial.licenseKey ?? 'REGK-KEY',
    ...partial,
  };
}

describe('billingSalesBulk', () => {
  const now = new Date('2026-08-11T12:00:00.000Z');

  it('uses current validUntil as base when still in the future', () => {
    const base = computeLicenseExtendBaseUtc('2026-09-01T00:00:00.000Z', now);
    expect(base.toISOString()).toBe('2026-09-01T00:00:00.000Z');
  });

  it('uses now as base when license already expired', () => {
    const base = computeLicenseExtendBaseUtc('2026-01-01T00:00:00.000Z', now);
    expect(base.toISOString()).toBe(now.toISOString());
  });

  it('adds days in UTC', () => {
    expect(computeExtendedValidUntilUtc('2026-09-01T00:00:00.000Z', 30, now)).toBe(
      '2026-10-01T00:00:00.000Z'
    );
    expect(computeExtendedValidUntilUtc('2026-01-01T00:00:00.000Z', 90, now)).toBe(
      '2026-11-09T12:00:00.000Z'
    );
  });

  it('dedupes extend targets by tenant and keeps latest validUntil', () => {
    const targets = buildBulkExtendTargets(
      [
        sale({
          id: 'a',
          tenantId: 't1',
          validUntilUtc: '2026-09-01T00:00:00.000Z',
          tenantName: 'Old',
        }),
        sale({
          id: 'b',
          tenantId: 't1',
          validUntilUtc: '2026-10-01T00:00:00.000Z',
          tenantName: 'New',
        }),
        sale({
          id: 'c',
          tenantId: 't2',
          validUntilUtc: '2026-08-20T00:00:00.000Z',
        }),
      ],
      30,
      now
    );

    expect(targets).toHaveLength(2);
    const t1 = targets.find((x) => x.tenantId === 't1');
    expect(t1?.sale.id).toBe('b');
    expect(t1?.nextValidUntilUtc).toBe('2026-10-31T00:00:00.000Z');
  });

  it('filters only active sales for revoke', () => {
    const rows = filterBulkRevokeSales([
      sale({ id: '1', status: 'active' }),
      sale({ id: '2', status: 'cancelled' }),
      sale({ id: undefined, status: 'active' }),
    ]);
    expect(rows.map((r) => r.id)).toEqual(['1']);
  });

  it('runs sequential bulk with progress and collects failures', async () => {
    const progress = vi.fn();
    const result = await runBulkSequential(
      ['ok', 'fail', 'ok2'],
      (item) => ({ id: item, label: item }),
      async (item) => {
        if (item === 'fail') throw new Error('boom');
      },
      progress
    );

    expect(result).toEqual({
      success: 2,
      failed: 1,
      total: 3,
      results: [
        { id: 'ok', label: 'ok', ok: true },
        { id: 'fail', label: 'fail', ok: false, errorMessage: 'boom' },
        { id: 'ok2', label: 'ok2', ok: true },
      ],
    });
    expect(progress).toHaveBeenCalledTimes(3);
  });
});

describe('exportLicenseSalesCsv', () => {
  it('builds CSV with header and sale rows', () => {
    const csv = buildLicenseSalesCsv([
      sale({
        invoiceNumber: 'INV-9',
        tenantName: 'Cafe "Central"',
        priceNet: 100,
        priceGross: 120,
      }),
    ]);
    expect(csv.split('\n')).toHaveLength(2);
    expect(csv).toContain('Invoice number');
    expect(csv).toContain('INV-9');
    expect(csv).toContain('"Cafe ""Central"""');
  });
});
