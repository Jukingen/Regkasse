/**
 * Admin truth surfaces — regression guard for operator-critical semantics.
 * Groups: register policy, FO queue / investigation URLs, replay↔incident↔verifications links,
 * receipt DTO display vs machine FK, provenance copy invariants, OpenAPI gap documentation.
 */
import { describe, expect, it } from 'vitest';

import type { ReceiptDTO } from '@/api/generated/model/receiptDTO';
import { mapReceiptDtoToDetail } from '@/features/receipts/api/forensics-client';
import {
  buildFinanzOnlineQueueInvestigationHref,
  buildIncidentInvestigationHref,
  buildReplayBatchDetailHref,
  buildVerificationsAuditHref,
  truncateInvestigationContextToken,
} from '@/shared/investigationNavigation';
import {
  OPERATOR_LINK_LABELS,
  OPERATOR_TRUTH_BADGE,
  OPERATOR_TRUTH_BADGE_KINDS,
} from '@/shared/operatorTruthCopy';
import { RKSv_ADMIN_CONTRACT_GAPS, viewReplayBatchTraceIds } from '@/shared/rksvAdminTruth';
import {
  formatRegisterDisplayLabel,
  isMissingAuthoritativeRegisterId,
  normalizeRegisterDisplayLabel,
} from '@/shared/utils/registerIdentity';

describe('truth-surfaces (1) register identity normalization — OpenAPI string fields', () => {
  it('isMissingAuthoritativeRegisterId is true for blank, nil UUID, and display-shaped ids', () => {
    expect(isMissingAuthoritativeRegisterId(undefined)).toBe(true);
    expect(isMissingAuthoritativeRegisterId('')).toBe(true);
    expect(isMissingAuthoritativeRegisterId('00000000-0000-0000-0000-000000000000')).toBe(true);
    expect(isMissingAuthoritativeRegisterId('KASSE-7')).toBe(true);
  });

  it('isMissingAuthoritativeRegisterId is false only for non-nil RFC-like UUID (contract: link/filter gate)', () => {
    const id = '11111111-1111-4111-8111-111111111111';
    expect(isMissingAuthoritativeRegisterId(id)).toBe(false);
  });

  it('formatRegisterDisplayLabel uses em dash placeholder when API omits display register text', () => {
    expect(formatRegisterDisplayLabel(undefined)).toBe('—');
    expect(formatRegisterDisplayLabel('  ')).toBe('—');
    expect(formatRegisterDisplayLabel('REG-42')).toBe('REG-42');
  });

  it('normalizeRegisterDisplayLabel returns undefined when empty so JSON-shaped rows stay lean', () => {
    expect(normalizeRegisterDisplayLabel(undefined)).toBeUndefined();
    expect(normalizeRegisterDisplayLabel('  ')).toBeUndefined();
    expect(normalizeRegisterDisplayLabel('A1')).toBe('A1');
  });
});

describe('truth-surfaces (2) reconciliation queue URL — buildFinanzOnlineQueueInvestigationHref (UI context params)', () => {
  const validReg = '11111111-1111-4111-8111-111111111111';
  const validPay = '22222222-2222-4222-8222-222222222222';
  const batchCorr = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';

  it('contract: invalid registerRowId must not appear as cashRegisterId even when batch correlation is present', () => {
    const href = buildFinanzOnlineQueueInvestigationHref({
      registerRowId: 'looks-like-kasse-01',
      investigationBatchCorrelationId: batchCorr,
    });
    expect(href).not.toMatch(/cashRegisterId=/);
    const u = new URL(href, 'http://x.test');
    expect(u.searchParams.get('investigationBatchCorrelationId')).toBe(batchCorr);
  });

  it('contract: omits investigationBatchCorrelationId when trimmed empty (no silent empty query)', () => {
    const href = buildFinanzOnlineQueueInvestigationHref({
      registerRowId: validReg,
      investigationBatchCorrelationId: '   ',
    });
    expect(
      new URL(href, 'http://x.test').searchParams.get('investigationBatchCorrelationId')
    ).toBeNull();
  });

  it('contract: focusPaymentId is accepted without link-safe register (payment UUID policy independent of register)', () => {
    const href = buildFinanzOnlineQueueInvestigationHref({
      registerRowId: 'not-uuid',
      focusPaymentId: validPay,
      investigationBatchCorrelationId: batchCorr,
    });
    const u = new URL(href, 'http://x.test');
    expect(u.searchParams.get('cashRegisterId')).toBeNull();
    expect(u.searchParams.get('focusPaymentId')).toBe(validPay);
    expect(u.searchParams.get('investigationBatchCorrelationId')).toBe(batchCorr);
  });

  it('passes list filter params through base path (server-side FO queue contract)', () => {
    const href = buildFinanzOnlineQueueInvestigationHref({
      registerRowId: validReg,
      fromUtc: '2025-01-01T00:00:00Z',
      toUtc: '2025-01-02T00:00:00Z',
      statusCsv: 'Pending,Failed',
    });
    const u = new URL(href, 'http://x.test');
    expect(u.searchParams.get('fromUtc')).toBe('2025-01-01T00:00:00Z');
    expect(u.searchParams.get('toUtc')).toBe('2025-01-02T00:00:00Z');
    expect(u.searchParams.get('status')).toBe('Pending,Failed');
    expect(u.searchParams.get('cashRegisterId')).toBe(validReg);
  });

  it('truncates investigationBatchCorrelationId to max context length in URL', () => {
    const long = `x${'y'.repeat(400)}`;
    const href = buildFinanzOnlineQueueInvestigationHref({
      investigationBatchCorrelationId: long,
    });
    const got = new URL(href, 'http://x.test').searchParams.get('investigationBatchCorrelationId');
    expect(got?.length).toBe(256);
    expect(got).toBe(truncateInvestigationContextToken(long));
  });
});

describe('truth-surfaces (3) derived vs persisted — ReceiptDTO mapping (forensics-client)', () => {
  it('contract: keeps cashRegisterId as API machine field and maps kassenID only to registerDisplayNumber', () => {
    const dto: ReceiptDTO = {
      receiptId: 'r1',
      cashRegisterId: 'REG-NOT-UUID-FK',
      kassenID: '33333333-3333-4333-8333-333333333333',
      date: '2025-01-01',
    };
    const d = mapReceiptDtoToDetail(dto);
    expect(d.cashRegisterId).toBe('REG-NOT-UUID-FK');
    expect(d.registerDisplayNumber).toBe('33333333-3333-4333-8333-333333333333');
  });

  it('contract: does not invent registerDisplayNumber when kassenID absent', () => {
    const dto: ReceiptDTO = { receiptId: 'r2', cashRegisterId: 'abc', kassenID: null };
    expect(mapReceiptDtoToDetail(dto).registerDisplayNumber).toBeUndefined();
  });
});

describe('truth-surfaces (4) optional-field fallbacks — investigation base paths', () => {
  it('buildReplayBatchDetailHref returns list base when correlation blank after trim', () => {
    expect(buildReplayBatchDetailHref('')).toBe('/rksv/replay-batch');
    expect(buildReplayBatchDetailHref('  ')).toBe('/rksv/replay-batch');
  });

  it('buildVerificationsAuditHref returns unfiltered base when correlation blank', () => {
    expect(buildVerificationsAuditHref('')).toBe('/rksv/verifications');
  });

  it('viewReplayBatchTraceIds omits incidentDeepLink when batch correlation missing (no fabricated id)', () => {
    expect(
      viewReplayBatchTraceIds({ correlationId: '', auditCorrelationId: null }).incidentDeepLink
    ).toBeUndefined();
    expect(viewReplayBatchTraceIds({ auditCorrelationId: 'x' }).incidentDeepLink).toBeUndefined();
  });
});

describe('truth-surfaces (5) provenance badge copy — operatorTruthCopy invariants', () => {
  it('every OPERATOR_TRUTH_BADGE_KIND has non-empty shortLabel, tooltip, and antColor', () => {
    for (const kind of OPERATOR_TRUTH_BADGE_KINDS) {
      const row = OPERATOR_TRUTH_BADGE[kind];
      expect(row.shortLabel.length).toBeGreaterThan(0);
      expect(row.tooltip.length).toBeGreaterThan(20);
      expect(row.antColor.length).toBeGreaterThan(0);
    }
  });

  it('display vs API vs diagnostic semantics remain verbally distinct (prevents copy merge regressions)', () => {
    expect(OPERATOR_TRUTH_BADGE.authoritative_api.tooltip).toMatch(/API-Feld/i);
    expect(OPERATOR_TRUTH_BADGE.display_only_label.tooltip).toMatch(/Anzeige|Textkennung/i);
    expect(OPERATOR_TRUTH_BADGE.derived_from_foreign_row.tooltip).toMatch(/anderen API-Zeile/i);
    expect(OPERATOR_TRUTH_BADGE.diagnostic_support.tooltip).toMatch(/Support|Analyse/i);
    expect(OPERATOR_TRUTH_BADGE.link_incomplete.tooltip).toMatch(/Deep-Links|UUID/i);
  });
});

describe('truth-surfaces (6) cross-links — replay batch trace URLs match shared investigation builders', () => {
  const batch = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
  const audit = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

  it('incidentDeepLink matches buildIncidentInvestigationHref(batch) for same DTO correlation', () => {
    const v = viewReplayBatchTraceIds({ correlationId: batch, auditCorrelationId: null });
    expect(v.incidentDeepLink).toBe(buildIncidentInvestigationHref(batch));
  });

  it('verificationsDeepLink matches buildVerificationsAuditHref when audit-only mode and audit id set', () => {
    const v = viewReplayBatchTraceIds(
      { correlationId: batch, auditCorrelationId: audit },
      { verificationsAuditOnly: true }
    );
    expect(v.verificationsDeepLink).toBe(buildVerificationsAuditHref(audit));
  });

  it('replay detail href uses path segment encoding consistent with buildReplayBatchDetailHref', () => {
    expect(buildReplayBatchDetailHref(batch)).toBe(
      `/rksv/replay-batch/${encodeURIComponent(batch)}`
    );
  });
});

describe('truth-surfaces — operator link labels stable for incident / replay / FO (German UI contract)', () => {
  it('OPERATOR_LINK_LABELS.replayBatchDetail implies single correlation path segment (wording + route shape)', () => {
    expect(OPERATOR_LINK_LABELS.replayBatchDetail.length).toBeGreaterThan(5);
    const path = buildReplayBatchDetailHref('cccccccc-cccc-4ccc-8ccc-cccccccccccc');
    expect(path).toMatch(/^\/rksv\/replay-batch\/[^?]+$/);
  });
});

describe('truth-surfaces — RKSv_ADMIN_CONTRACT_GAPS (documented OpenAPI gaps, no backend simulation)', () => {
  it('retains expected gap keys so UI comments and operator docs stay aligned', () => {
    expect(RKSv_ADMIN_CONTRACT_GAPS.invoiceDetailProvenance).toMatch(
      /provenance|payment-derived|OpenAPI/i
    );
    expect(RKSv_ADMIN_CONTRACT_GAPS.replayBatchPaymentRegisterFk).toMatch(
      /ReplayBatchPayment|register/i
    );
    expect(RKSv_ADMIN_CONTRACT_GAPS.invoiceListRowOrigin).toMatch(
      /InvoiceListItemDto|listRowOrigin/i
    );
    expect(RKSv_ADMIN_CONTRACT_GAPS.receiptSignatureDebugResponse).toMatch(
      /signature-debug|OpenAPI/i
    );
  });
});
