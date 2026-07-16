import type { AuditLogEntryDto } from '@/api/generated/model';
import { parseAuditJsonField, parseAuditReason } from '@/features/audit-logs/utils/parseAuditJsonField';

type QuickUserAuditDetails = {
    email?: string;
    role?: string;
    tenantId?: string;
};

function parseQuickUserDetails(record: AuditLogEntryDto): QuickUserAuditDetails | null {
    const raw = record.requestData?.trim();
    if (!raw) return null;
    try {
        return JSON.parse(raw) as QuickUserAuditDetails;
    } catch {
        return null;
    }
}

function extractTenantSlug(record: AuditLogEntryDto, details: QuickUserAuditDetails | null): string | null {
    const desc = record.description ?? '';
    const slugMatch = /für Mandant '([^']+)'/.exec(desc);
    if (slugMatch?.[1]) return slugMatch[1];
    if (details?.email) {
        const emailMatch = /@([^.]+)\.regkasse\.at$/i.exec(details.email);
        if (emailMatch?.[1]) return emailMatch[1];
    }
    return null;
}

function formatUserNameChangeDescription(
    record: AuditLogEntryDto,
    translate: (key: string, params?: Record<string, string>) => string,
): string | null {
    const oldName =
        parseAuditJsonField(record.oldValues, 'UserName') ??
        parseAuditJsonField(record.oldValues, 'userName');
    const newName =
        parseAuditJsonField(record.newValues, 'UserName') ??
        parseAuditJsonField(record.newValues, 'userName');

    if (oldName || newName) {
        return translate('common.auditLogs.userNameChangedDescription', {
            old: oldName ?? '—',
            new: newName ?? '—',
        });
    }

    const description = record.description?.trim();
    return description || null;
}

/**
 * Operator-facing description for audit log table (German for quick-user rows).
 */
export function formatAuditLogDescription(
    record: AuditLogEntryDto,
    translate: (key: string, params?: Record<string, string>) => string,
): string {
    const action = record.action?.trim();
    if (action === 'USER_NAME_CHANGE') {
        return formatUserNameChangeDescription(record, translate) ?? '';
    }

    if (action === 'TENANT_QUICK_USER_CREATED') {
        const fromDescription = record.description?.trim();
        if (fromDescription) return fromDescription;

        const details = parseQuickUserDetails(record);
        const email = details?.email ?? record.entityName ?? '—';
        const role = details?.role ?? '—';
        const slug =
            extractTenantSlug(record, details) ??
            (details?.email ? /@([^.]+)\.regkasse\.at$/i.exec(details.email)?.[1] : null) ??
            '—';
        return translate('common.auditLogs.quickUserCreatedDescription', { email, role, slug });
    }

    if (action === 'TagesabschlussBackdatedCreated') {
        const closingDate =
            parseAuditJsonField(record.requestData, 'closingDate') ??
            parseAuditJsonField(record.requestData, 'ClosingDate');
        const reason =
            parseAuditJsonField(record.requestData, 'backdatedReason') ??
            parseAuditJsonField(record.requestData, 'reason');
        const daysLate = parseAuditJsonField(record.requestData, 'daysLate');
        if (closingDate || reason) {
            return translate('common.auditLogs.tagesabschlussBackdatedDescription', {
                date: closingDate ?? '—',
                reason: reason ?? '—',
                daysLate: daysLate ?? '—',
            });
        }
    }

    return record.description?.trim() ?? '';
}

/** Optional operator reason from metadata/notes (e.g. username change justification). */
export function formatAuditLogReason(record: AuditLogEntryDto): string | null {
    return parseAuditReason(record);
}
