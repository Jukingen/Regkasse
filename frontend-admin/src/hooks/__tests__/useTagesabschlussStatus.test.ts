import { describe, expect, it } from 'vitest';

import { computeIsClosingRequired } from '@/hooks/useTagesabschlussStatus';

describe('computeIsClosingRequired', () => {
    it('is true when can close and transactions exist', () => {
        expect(computeIsClosingRequired({ canClose: true, transactionCount: 3 })).toBe(true);
    });

    it('is false when already closed even with transactions', () => {
        expect(computeIsClosingRequired({ canClose: false, transactionCount: 5 })).toBe(false);
    });

    it('is false when can close but no transactions (no noise reminder)', () => {
        expect(computeIsClosingRequired({ canClose: true, transactionCount: 0 })).toBe(false);
    });
});
