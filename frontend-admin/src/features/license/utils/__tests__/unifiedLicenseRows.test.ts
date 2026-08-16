import { describe, expect, it } from 'vitest';

import type { IssuedLicenseListItemDto } from '@/api/manual/adminLicense';
import type { LicenseSaleResponse } from '@/api/generated/model';
import {
  mapIssuedLicenseToUnifiedRow,
  mapLicenseSaleToUnifiedRow,
  mergeUnifiedLicenseRows,
} from '@/features/license/utils/unifiedLicenseRows';

function issued(overrides: Partial<IssuedLicenseListItemDto> = {}): IssuedLicenseListItemDto {
  return {
    id: 'iss-1',
    licenseKey: 'REGK-20990101-system-ABCDEF12',
    customerName: 'Acme GmbH',
    expiryAtUtc: '2099-01-01T23:59:59Z',
    requireFingerprint: false,
    machineHashHex: null,
    issuedAtUtc: '2026-01-01T00:00:00Z',
    issuedByUserId: null,
    isRevoked: false,
    revokedAtUtc: null,
    revocationReason: null,
    isCancelled: false,
    ...overrides,
  };
}

function sale(overrides: Partial<LicenseSaleResponse> = {}): LicenseSaleResponse {
  return {
    id: 'sale-1',
    licenseKey: 'REGK-20270101-cafe-A7F3K2D9',
    tenantName: 'Cafe Linz',
    tenantSlug: 'cafe',
    validUntilUtc: '2027-01-01T00:00:00Z',
    status: 'active',
    ...overrides,
  };
}

describe('unifiedLicenseRows', () => {
  it('maps issued (server) licenses', () => {
    expect(mapIssuedLicenseToUnifiedRow(issued({ isRevoked: true }))).toMatchObject({
      kind: 'server',
      status: 'revoked',
      displayName: 'Acme GmbH',
    });
  });

  it('maps tenant sales and drops rows without a key', () => {
    expect(mapLicenseSaleToUnifiedRow(sale())).toMatchObject({
      kind: 'tenant',
      displayName: 'Cafe Linz',
      tenantSlug: 'cafe',
    });
    expect(mapLicenseSaleToUnifiedRow(sale({ licenseKey: null }))).toBeNull();
  });

  it('merges, filters by kind, and searches', () => {
    const rows = mergeUnifiedLicenseRows({
      issued: [issued()],
      sales: [sale()],
      kindFilter: 'tenant',
      search: 'cafe',
    });
    expect(rows).toHaveLength(1);
    expect(rows[0]?.kind).toBe('tenant');
  });

  it('shows server and tenant licenses together for the management hub', () => {
    const rows = mergeUnifiedLicenseRows({
      issued: [
        issued({
          id: 'iss-system',
          licenseKey: 'REGK-20261231-system-C8YEM41L',
          customerName: 'Server',
          expiryAtUtc: '2026-12-31T23:59:59Z',
        }),
      ],
      sales: [
        sale({
          id: 'sale-dev',
          licenseKey: 'REGK-20261231-dev-A4WCG52H',
          tenantName: 'Dev Mandant',
          tenantSlug: 'dev',
          validUntilUtc: '2026-12-31T23:59:59Z',
        }),
      ],
      kindFilter: 'all',
    });

    expect(rows.map((row) => row.kind).sort()).toEqual(['server', 'tenant']);
    expect(rows.some((row) => row.licenseKey === 'REGK-20261231-system-C8YEM41L')).toBe(true);
    expect(rows.some((row) => row.licenseKey === 'REGK-20261231-dev-A4WCG52H' && row.tenantSlug === 'dev')).toBe(
      true
    );
  });
});
