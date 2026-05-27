import { describe, expect, it, vi } from 'vitest';

import type { AuditLogEntryDto } from '@/api/generated/model';
import {
    formatAuditLogDescription,
    formatAuditLogReason,
} from '@/features/audit-logs/utils/formatAuditLogDescription';

const t = vi.fn((key: string, params?: Record<string, string>) => {
    if (key === 'common.auditLogs.userNameChangedDescription' && params) {
        return `changed ${params.old} -> ${params.new}`;
    }
    return key;
});

describe('formatAuditLogDescription', () => {
    it('formats USER_NAME_CHANGE from oldValues/newValues JSON', () => {
        const record: AuditLogEntryDto = {
            action: 'USER_NAME_CHANGE',
            oldValues: '{"UserName":"cashier_old"}',
            newValues: '{"UserName":"cashier_new"}',
            description: 'Username changed',
        };
        expect(formatAuditLogDescription(record, t)).toBe('changed cashier_old -> cashier_new');
    });

    it('falls back to description when JSON values are missing', () => {
        const record: AuditLogEntryDto = {
            action: 'USER_NAME_CHANGE',
            description: 'Benutzername geändert',
        };
        expect(formatAuditLogDescription(record, t)).toBe('Benutzername geändert');
    });
});

describe('formatAuditLogReason', () => {
    it('reads reason from metadata JSON', () => {
        const record: AuditLogEntryDto = {
            metadata: '{"reason":"Typo correction"}',
        };
        expect(formatAuditLogReason(record)).toBe('Typo correction');
    });
});
