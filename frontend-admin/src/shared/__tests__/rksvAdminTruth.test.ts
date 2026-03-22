import { describe, expect, it } from 'vitest';
import type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';
import { viewInvoiceListRegister, viewReplayBatchTraceIds } from '@/shared/rksvAdminTruth';

describe('viewInvoiceListRegister', () => {
    it('passes through raw cashRegisterId and derives link id only for valid UUID', () => {
        const row: InvoiceListItemDto = {
            cashRegisterId: 'not-a-uuid',
            kassenId: 'REG-1',
        };
        const v = viewInvoiceListRegister(row);
        expect(v.apiCashRegisterId).toBe('not-a-uuid');
        expect(v.kassenDisplay).toBe('REG-1');
        expect(v.finanzQueueRegisterRowId).toBeUndefined();
        expect(v.registerFkRawNotLinkSafe).toBe(true);
    });

    it('accepts canonical register UUID for FO queue links', () => {
        const id = '11111111-1111-4111-8111-111111111111';
        const row: InvoiceListItemDto = { cashRegisterId: id, kassenId: null };
        const v = viewInvoiceListRegister(row);
        expect(v.finanzQueueRegisterRowId).toBe(id);
        expect(v.registerFkRawNotLinkSafe).toBe(false);
    });
});

describe('viewReplayBatchTraceIds', () => {
    it('builds incident link from batch correlation only when present', () => {
        expect(
            viewReplayBatchTraceIds({
                correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
                auditCorrelationId: null,
            }).incidentDeepLink,
        ).toContain('correlationId=');
        expect(viewReplayBatchTraceIds({}).incidentDeepLink).toBeUndefined();
    });

    it('omits verifications link when audit id missing and verificationsAuditOnly', () => {
        const v = viewReplayBatchTraceIds(
            { correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', auditCorrelationId: null },
            { verificationsAuditOnly: true },
        );
        expect(v.verificationsDeepLink).toBeUndefined();
    });

    it('falls back verifications to batch correlation when audit missing and not audit-only', () => {
        const v = viewReplayBatchTraceIds({
            correlationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
            auditCorrelationId: null,
        });
        expect(v.verificationsDeepLink).toContain('correlationId=');
    });
});
