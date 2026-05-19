import { describe, expect, it } from 'vitest';
import {
    parseAuditLogStatusFromUrl,
    toAuditLogStatusApiParam,
    toAuditLogStatusUrlParam,
} from '@/features/audit-logs/constants/auditLogFilters';

describe('audit log status URL/API mapping', () => {
    it('parses Failure URL alias to Failed', () => {
        expect(parseAuditLogStatusFromUrl('Failure')).toBe('Failed');
    });

    it('serializes Failed as Failure in shareable URLs', () => {
        expect(toAuditLogStatusUrlParam('Failed')).toBe('Failure');
        expect(toAuditLogStatusUrlParam('Success')).toBe('Success');
    });

    it('sends Failed to the API query', () => {
        expect(toAuditLogStatusApiParam('Failed')).toBe('Failed');
    });
});
