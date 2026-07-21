import { describe, expect, it } from 'vitest';

import type { Invoice } from '@/api/generated/model/invoice';
import {
  invoiceProvenanceUiFacet,
  registerDeepLinkEligibleBadgeKind,
} from '@/shared/adminTruthFacets';
import { viewFinanzReconciliationRegister } from '@/shared/rksvAdminTruth';

describe('registerDeepLinkEligibleBadgeKind', () => {
  it('authoritative_api only when linkSafeUuid is set', () => {
    expect(registerDeepLinkEligibleBadgeKind({ linkSafeUuid: undefined })).toBe('link_incomplete');
    expect(
      registerDeepLinkEligibleBadgeKind({
        linkSafeUuid: '11111111-1111-4111-8111-111111111111',
      })
    ).toBe('authoritative_api');
  });

  it('matches FinanzOnline reconciliation register view (raw FK may differ from link-safe UUID)', () => {
    const nonUuidRow = viewFinanzReconciliationRegister({ cashRegisterId: 'KASSE-LABEL-ONLY' });
    expect(nonUuidRow.apiCashRegisterId).toBe('KASSE-LABEL-ONLY');
    expect(
      registerDeepLinkEligibleBadgeKind({ linkSafeUuid: nonUuidRow.finanzQueueRegisterRowId })
    ).toBe('link_incomplete');

    const uuidRow = viewFinanzReconciliationRegister({
      cashRegisterId: '11111111-1111-4111-8111-111111111111',
    });
    expect(
      registerDeepLinkEligibleBadgeKind({ linkSafeUuid: uuidRow.finanzQueueRegisterRowId })
    ).toBe('authoritative_api');
  });
});

function minimalInvoice(overrides: Partial<Invoice> = {}): Invoice {
  return {
    cashRegisterId: '',
    companyAddress: '',
    companyName: '',
    companyTaxNumber: '',
    createdAt: '',
    dueDate: '',
    invoiceDate: '',
    invoiceNumber: 'X',
    kassenId: '',
    paidAmount: 0,
    remainingAmount: 0,
    subtotal: 0,
    taxAmount: 0,
    taxDetails: {},
    totalAmount: 0,
    tseSignature: '',
    tseTimestamp: '',
    status: 0,
    ...overrides,
  };
}

describe('invoiceProvenanceUiFacet', () => {
  it('contract_incomplete when invoiceDataProvenance absent', () => {
    const f = invoiceProvenanceUiFacet(minimalInvoice());
    expect(f.kind).toBe('contract_incomplete_no_response_field');
    if (f.kind === 'contract_incomplete_no_response_field') {
      expect(f.operatorCopyKey).toBe('detailProvenanceFooter');
    }
  });

  it('explicit_backend_string when JSON carries invoiceDataProvenance', () => {
    const inv = { ...minimalInvoice(), invoiceDataProvenance: 'Persisted' } as Invoice;
    const f = invoiceProvenanceUiFacet(inv);
    expect(f.kind).toBe('explicit_backend_string');
    if (f.kind === 'explicit_backend_string') {
      expect(f.raw).toBe('Persisted');
      expect(f.operatorLabel.length).toBeGreaterThan(3);
      expect(f.typingNote).toBe('invoiceDataProvenance_not_on_orval_invoice_until_openapi');
    }
  });
});
