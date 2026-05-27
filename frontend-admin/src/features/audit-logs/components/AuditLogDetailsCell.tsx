'use client';

import { Tooltip, Typography } from 'antd';

import type { AuditLogEntryDto } from '@/api/generated/model';
import {
    formatAuditLogDescription,
    formatAuditLogReason,
} from '@/features/audit-logs/utils/formatAuditLogDescription';
import { parseAuditJsonField } from '@/features/audit-logs/utils/parseAuditJsonField';

type Props = {
    record: AuditLogEntryDto;
    translate: (key: string, params?: Record<string, string>) => string;
};

export function AuditLogDetailsCell({ record, translate }: Props) {
    const action = record.action?.trim();

    if (action === 'USER_NAME_CHANGE') {
        const oldName =
            parseAuditJsonField(record.oldValues, 'UserName') ??
            parseAuditJsonField(record.oldValues, 'userName');
        const newName =
            parseAuditJsonField(record.newValues, 'UserName') ??
            parseAuditJsonField(record.newValues, 'userName');
        const reason = formatAuditLogReason(record);
        const fallback = formatAuditLogDescription(record, translate) || record.description?.trim();

        if (oldName || newName) {
            const detail = (
                <span>
                    {translate('common.auditLogs.userNameChangedPrefix')}{' '}
                    <Typography.Text strong>{oldName ?? '—'}</Typography.Text>
                    {' → '}
                    <Typography.Text strong>{newName ?? '—'}</Typography.Text>
                    {reason ? (
                        <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                            ({reason})
                        </Typography.Text>
                    ) : null}
                </span>
            );
            return (
                <Tooltip title={fallback || undefined}>
                    <Typography.Text ellipsis style={{ maxWidth: 320, display: 'block' }}>
                        {detail}
                    </Typography.Text>
                </Tooltip>
            );
        }
    }

    const detailText = formatAuditLogDescription(record, translate) || record.description?.trim();
    if (!detailText) return <Typography.Text type="secondary">—</Typography.Text>;

    return (
        <Tooltip title={detailText}>
            <Typography.Text ellipsis style={{ maxWidth: 320, display: 'block' }}>
                {detailText}
            </Typography.Text>
        </Tooltip>
    );
}
