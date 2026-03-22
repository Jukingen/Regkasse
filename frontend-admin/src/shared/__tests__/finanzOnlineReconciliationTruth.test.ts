import { describe, expect, it } from 'vitest';
import type { FinanzOnlineReconciliationItemDto } from '@/api/generated/model/finanzOnlineReconciliationItemDto';
import {
    FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS,
    finanzOnlineRowTechnicalResponseSummary,
} from '@/shared/finanzOnlineReconciliationTruth';

describe('finanzOnlineRowTechnicalResponseSummary', () => {
    it('returns trimmed finanzOnlineError when present', () => {
        const row: FinanzOnlineReconciliationItemDto = {
            finanzOnlineError: '  http 500  ',
        };
        expect(finanzOnlineRowTechnicalResponseSummary(row)).toBe('http 500');
    });

    it('returns undefined when error blank', () => {
        expect(finanzOnlineRowTechnicalResponseSummary({ finanzOnlineError: null })).toBeUndefined();
        expect(finanzOnlineRowTechnicalResponseSummary({ finanzOnlineError: '   ' })).toBeUndefined();
    });
});

describe('FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS', () => {
    it('lists row-level fields that require OpenAPI/backend extension', () => {
        expect(FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS).toContain('correlationId');
        expect(FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS).toContain('errorClassPerRow');
        expect(FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS).toContain('retryableFlag');
        expect(FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS.length).toBe(6);
    });
});
