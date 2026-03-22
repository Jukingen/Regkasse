import { describe, expect, it } from 'vitest';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildReplayBatchDetailHref,
    buildVerificationsAuditHref,
    truncateInvestigationContextToken,
} from '@/shared/investigationNavigation';

describe('buildIncidentInvestigationHref', () => {
    it('encodes correlation for query', () => {
        expect(buildIncidentInvestigationHref('aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee')).toBe(
            '/rksv/incident?correlationId=aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
        );
    });

    it('falls back to base when empty', () => {
        expect(buildIncidentInvestigationHref('  ')).toBe('/rksv/incident');
    });
});

describe('buildReplayBatchDetailHref', () => {
    it('encodes path segment', () => {
        expect(buildReplayBatchDetailHref('aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee')).toBe(
            '/rksv/replay-batch/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
        );
    });
});

describe('buildVerificationsAuditHref', () => {
    it('puts correlation in audit log filter query', () => {
        const id = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
        expect(buildVerificationsAuditHref(id)).toBe(`/rksv/verifications?correlationId=${encodeURIComponent(id)}`);
    });
});

describe('buildFinanzOnlineQueueInvestigationHref', () => {
    it('adds focus payment only for valid UUID', () => {
        const href = buildFinanzOnlineQueueInvestigationHref({
            registerRowId: '11111111-1111-4111-8111-111111111111',
            focusPaymentId: '22222222-2222-4222-8222-222222222222',
            investigationBatchCorrelationId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
        });
        expect(href).toContain('cashRegisterId=11111111-1111-4111-8111-111111111111');
        expect(href).toContain('focusPaymentId=22222222-2222-4222-8222-222222222222');
        expect(href).toContain('investigationBatchCorrelationId=');
    });

    it('drops invalid focus payment id', () => {
        const href = buildFinanzOnlineQueueInvestigationHref({
            focusPaymentId: 'not-a-uuid',
        });
        expect(href).not.toContain('focusPaymentId=');
    });
});

describe('truncateInvestigationContextToken', () => {
    it('caps length', () => {
        const long = 'x'.repeat(400);
        expect(truncateInvestigationContextToken(long).length).toBe(256);
    });
});
