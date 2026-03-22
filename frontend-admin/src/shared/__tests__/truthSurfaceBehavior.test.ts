/**
 * Truth-surface semantics: register FK vs display labels, investigation URLs, replay/incident trace policy.
 * Depends on Orval DTO field names (InvoiceListItemDto, FinanzOnlineReconciliationItemDto, ReplayBatchDetailResponse).
 */

import { describe, expect, it } from 'vitest';
import type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';
import type { FinanzOnlineReconciliationItemDto } from '@/api/generated/model/finanzOnlineReconciliationItemDto';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildReplayBatchDetailHref,
    buildVerificationsAuditHref,
} from '@/shared/investigationNavigation';
import {
    viewFinanzReconciliationRegister,
    viewInvoiceListRegister,
    viewReplayBatchTraceIds,
} from '@/shared/rksvAdminTruth';

describe('truth: invoice list register (OpenAPI InvoiceListItemDto)', () => {
    it('does not use kassenId as machine FK for queue links even when it looks like a UUID', () => {
        const row: InvoiceListItemDto = {
            kassenId: '11111111-1111-4111-8111-111111111111',
            cashRegisterId: undefined,
        };
        const v = viewInvoiceListRegister(row);
        expect(v.kassenDisplay).toContain('11111111');
        expect(v.finanzQueueRegisterRowId).toBeUndefined();
        expect(v.apiCashRegisterId).toBeUndefined();
        expect(v.registerFkRawNotLinkSafe).toBe(false);
    });

    it('keeps non-UUID cashRegisterId visible and does not emit link-safe queue id', () => {
        const row: InvoiceListItemDto = { cashRegisterId: 'REG-DISPLAY-ONLY', kassenId: 'K1' };
        const v = viewInvoiceListRegister(row);
        expect(v.apiCashRegisterId).toBe('REG-DISPLAY-ONLY');
        expect(v.finanzQueueRegisterRowId).toBeUndefined();
        expect(v.registerFkRawNotLinkSafe).toBe(true);
    });
});

describe('truth: FinanzOnline reconciliation row register (OpenAPI FinanzOnlineReconciliationItemDto)', () => {
    it('when cashRegisterId absent, no link-safe register id is invented', () => {
        const row: FinanzOnlineReconciliationItemDto = { paymentId: 'p1', receiptNumber: 'R1' };
        const v = viewFinanzReconciliationRegister(row);
        expect(v.apiCashRegisterId).toBeUndefined();
        expect(v.finanzQueueRegisterRowId).toBeUndefined();
        expect(v.registerFkRawNotLinkSafe).toBe(false);
    });

    it('when cashRegisterId is invalid shape, raw remains for display policy but link id stays undefined', () => {
        const row: FinanzOnlineReconciliationItemDto = { cashRegisterId: 'not-uuid' };
        const v = viewFinanzReconciliationRegister(row);
        expect(v.apiCashRegisterId).toBe('not-uuid');
        expect(v.finanzQueueRegisterRowId).toBeUndefined();
        expect(v.registerFkRawNotLinkSafe).toBe(true);
    });
});

describe('truth: investigation cross-links share correlation without merging APIs', () => {
    const correlation = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';

    it('incident, replay-batch path, and verifications query encode the same correlation token', () => {
        expect(buildIncidentInvestigationHref(correlation)).toBe(
            `/rksv/incident?correlationId=${encodeURIComponent(correlation)}`,
        );
        expect(buildReplayBatchDetailHref(correlation)).toBe(
            `/rksv/replay-batch/${encodeURIComponent(correlation)}`,
        );
        expect(buildVerificationsAuditHref(correlation)).toBe(
            `/rksv/verifications?correlationId=${encodeURIComponent(correlation)}`,
        );
    });

    it('encodes characters that would break query strings', () => {
        const tricky = 'corr&x=1';
        expect(buildIncidentInvestigationHref(tricky)).toContain(encodeURIComponent(tricky));
    });

    it('FO investigation URL carries batch correlation as display-only param alongside valid register UUID', () => {
        const reg = '11111111-1111-4111-8111-111111111111';
        const href = buildFinanzOnlineQueueInvestigationHref({
            registerRowId: reg,
            investigationBatchCorrelationId: correlation,
        });
        const params = new URL(href, 'http://local.test').searchParams;
        expect(params.get('cashRegisterId')).toBe(reg);
        expect(params.get('investigationBatchCorrelationId')).toBe(correlation);
    });
});

describe('truth: replay batch trace ids (OpenAPI ReplayBatchDetailResponse + audit policy)', () => {
    const batchId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
    const auditId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';

    it('incidentDeepLink always uses batch correlation when present', () => {
        const v = viewReplayBatchTraceIds({ correlationId: batchId, auditCorrelationId: auditId });
        expect(v.incidentDeepLink).toBe(`/rksv/incident?correlationId=${encodeURIComponent(batchId)}`);
    });

    it('when verificationsAuditOnly, omits verifications link if audit correlation missing (does not silently substitute batch id)', () => {
        const v = viewReplayBatchTraceIds(
            { correlationId: batchId, auditCorrelationId: null },
            { verificationsAuditOnly: true },
        );
        expect(v.verificationsDeepLink).toBeUndefined();
    });

    it('when not audit-only, verifications falls back to batch correlation for deep link', () => {
        const v = viewReplayBatchTraceIds({ correlationId: batchId, auditCorrelationId: null });
        expect(v.verificationsDeepLink).toContain(encodeURIComponent(batchId));
    });

    it('when audit id present, verifications prefers audit correlation', () => {
        const v = viewReplayBatchTraceIds({ correlationId: batchId, auditCorrelationId: auditId });
        expect(v.verificationsDeepLink).toContain(encodeURIComponent(auditId));
        expect(v.verificationsDeepLink).not.toContain(encodeURIComponent(batchId));
    });
});
