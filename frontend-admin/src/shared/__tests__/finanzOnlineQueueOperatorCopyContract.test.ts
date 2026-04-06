import { describe, expect, it } from 'vitest';
import deFinanzOnlineReconciliation from '@/i18n/locales/de/finanzOnlineReconciliation.json';
import { OPERATOR_FO_OPERATIONS_PAGE_COPY } from '@/shared/operatorTruthCopy';

/**
 * Contract tests: queue/outbox operator copy and i18n keys used for
 * derived-vs-outbox warnings and simulated transport surfacing.
 * Catches accidental removals without rendering full Next.js pages.
 */
describe('FinanzOnline queue / operations operator copy contract', () => {
    it('exposes simulated transport note for diagnostics page', () => {
        expect(OPERATOR_FO_OPERATIONS_PAGE_COPY.simulatedTransportNote.length).toBeGreaterThan(20);
        expect(OPERATOR_FO_OPERATIONS_PAGE_COPY.simulatedTransportNote).toContain('simuliert');
    });

    it('keeps derived legacy truth alert copy (reconciliation ≠ outbox truth)', () => {
        const q = deFinanzOnlineReconciliation.queuePage as {
            derivedLegacyTruthAlert?: { title?: string; description?: string };
        };
        expect(q.derivedLegacyTruthAlert?.title?.length).toBeGreaterThan(10);
        expect(q.derivedLegacyTruthAlert?.description?.length).toBeGreaterThan(40);
        expect(q.derivedLegacyTruthAlert?.description).toContain('Outbox');
    });

    it('keeps simulated list-surface badge and authoritative outbox expand strings', () => {
        const q = deFinanzOnlineReconciliation.queuePage as {
            transportSurfaceBadge?: { simulated?: string };
            expandRow?: { authoritativeOutboxTitle?: string; protocolSuccessSimulatedExpandNote?: string };
        };
        expect(q.transportSurfaceBadge?.simulated?.length).toBeGreaterThan(10);
        expect(q.expandRow?.authoritativeOutboxTitle?.length).toBeGreaterThan(5);
        expect(q.expandRow?.protocolSuccessSimulatedExpandNote?.length).toBeGreaterThan(20);
    });
});
