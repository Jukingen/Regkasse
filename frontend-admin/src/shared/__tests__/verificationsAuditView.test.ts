/**
 * RKSV Audit-Spur: keyword sample, entity links, status presentation, honest copy exports.
 */

import { describe, expect, it } from 'vitest';
import type { AuditLogEntryDto } from '@/api/generated/model';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import { buildVerificationsAuditHref } from '@/shared/investigationNavigation';
import {
    auditLogMatchesVerificationsKeywordSample,
    viewAuditLogEntityDeepLinks,
    viewAuditLogStatusPresentation,
} from '@/shared/verificationsAuditView';

const validPaymentId = '22222222-2222-4222-8222-222222222222';
const validReceiptId = '33333333-3333-4333-8333-333333333333';

describe('auditLogMatchesVerificationsKeywordSample', () => {
    it('includes signature-related actions', () => {
        expect(
            auditLogMatchesVerificationsKeywordSample({
                action: 'RECEIPT_SAVED',
                entityType: 'Cart',
            } as AuditLogEntryDto),
        ).toBe(false);
        expect(
            auditLogMatchesVerificationsKeywordSample({
                action: 'TSE_SIGNATURE_OK',
                entityType: 'Payment',
            } as AuditLogEntryDto),
        ).toBe(true);
    });

    it('does not treat arbitrary strings as matches (no false positives)', () => {
        expect(
            auditLogMatchesVerificationsKeywordSample({
                action: 'LOGIN',
                entityType: 'User',
            } as AuditLogEntryDto),
        ).toBe(false);
    });
});

describe('viewAuditLogEntityDeepLinks', () => {
    it('emits payment list href for Payment + link-safe UUID', () => {
        const v = viewAuditLogEntityDeepLinks({
            entityType: 'Payment',
            entityId: validPaymentId,
        } as AuditLogEntryDto);
        expect(v.paymentListHref).toBe(`/payments?paymentId=${encodeURIComponent(validPaymentId)}`);
        expect(v.receiptDetailHref).toBeUndefined();
    });

    it('emits receipt detail href for Receipt + link-safe UUID', () => {
        const v = viewAuditLogEntityDeepLinks({
            entityType: 'Receipt',
            entityId: validReceiptId,
        } as AuditLogEntryDto);
        expect(v.receiptDetailHref).toBe(`/receipts/${encodeURIComponent(validReceiptId)}`);
        expect(v.paymentListHref).toBeUndefined();
    });

    it('omits links for nil UUID or non-machine ids', () => {
        expect(viewAuditLogEntityDeepLinks({ entityType: 'Payment', entityId: '00000000-0000-0000-0000-000000000000' } as AuditLogEntryDto)).toEqual({});
        expect(viewAuditLogEntityDeepLinks({ entityType: 'Payment', entityId: 'not-uuid' } as AuditLogEntryDto)).toEqual({});
    });

    it('omits links for Invoice rows (no authoritative invoice deep route here)', () => {
        expect(
            viewAuditLogEntityDeepLinks({
                entityType: 'Invoice',
                entityId: validPaymentId,
            } as AuditLogEntryDto),
        ).toEqual({});
    });
});

describe('viewAuditLogStatusPresentation', () => {
    it('maps ordinal 0 to Success', () => {
        const p = viewAuditLogStatusPresentation(0);
        expect(p.label).toBe('Success');
        expect(p.antColor).toBe('success');
    });

    it('degrades unknown numeric codes without throwing', () => {
        const p = viewAuditLogStatusPresentation(99);
        expect(p.label).toBe('Status(99)');
        expect(p.antColor).toBe('default');
    });
});

describe('correlation filter URL (investigation handoff)', () => {
    it('encodes correlation for verifications route', () => {
        const id = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
        expect(buildVerificationsAuditHref(id)).toContain(encodeURIComponent(id));
    });
});

describe('OPERATOR_VERIFICATIONS_COPY (no misleading pipeline title)', () => {
    it('page title does not claim dedicated verification results', () => {
        const t = OPERATOR_VERIFICATIONS_COPY.pageTitle.toLowerCase();
        expect(t).not.toContain('last 100');
        expect(t).not.toContain('verification results');
        expect(t).toContain('audit');
    });
});
