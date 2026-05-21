import type { AuditLogEntryDto } from '@/api/generated/model';

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

/**
 * Operator-facing description for audit log table (German for quick-user rows).
 */
export function formatAuditLogDescription(
    record: AuditLogEntryDto,
    translate: (key: string, params?: Record<string, string>) => string,
): string {
    const action = record.action?.trim();
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

    return record.description?.trim() ?? '';
}
