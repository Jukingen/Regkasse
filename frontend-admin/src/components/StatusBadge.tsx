'use client';

import { Tag } from 'antd';

import { useI18n } from '@/i18n';

export type StatusType =
    | 'active'
    | 'inactive'
    | 'pending'
    | 'cancelled'
    | 'completed'
    | 'expired'
    | 'open'
    | 'closed'
    | 'success'
    | 'error'
    | 'warning'
    | 'info'
    | 'suspended'
    | 'deleted';

export type StatusBadgeProps = {
    status: StatusType;
    label?: string;
    size?: 'small' | 'default';
};

const statusColor: Record<StatusType, string> = {
    active: 'green',
    inactive: 'default',
    pending: 'orange',
    cancelled: 'red',
    completed: 'blue',
    expired: 'red',
    open: 'green',
    closed: 'default',
    success: 'green',
    error: 'red',
    warning: 'orange',
    info: 'blue',
    suspended: 'orange',
    deleted: 'red',
};

const statusLabelKey: Record<StatusType, `common.status.${StatusType}`> = {
    active: 'common.status.active',
    inactive: 'common.status.inactive',
    pending: 'common.status.pending',
    cancelled: 'common.status.cancelled',
    completed: 'common.status.completed',
    expired: 'common.status.expired',
    open: 'common.status.open',
    closed: 'common.status.closed',
    success: 'common.status.success',
    error: 'common.status.error',
    warning: 'common.status.warning',
    info: 'common.status.info',
    suspended: 'common.status.suspended',
    deleted: 'common.status.deleted',
};

const KNOWN_STATUSES = new Set<string>(Object.keys(statusColor));

/** Map free-form / API status strings onto the shared StatusBadge vocabulary. */
export function resolveStatusType(status: string | null | undefined): StatusType | null {
    if (!status) return null;
    const normalized = status.trim().toLowerCase();
    return KNOWN_STATUSES.has(normalized) ? (normalized as StatusType) : null;
}

export function StatusBadge({ status, label, size = 'default' }: StatusBadgeProps) {
    const { t } = useI18n();
    const color = statusColor[status];
    const text = label ?? t(statusLabelKey[status]);

    return (
        <Tag
            color={color}
            variant="filled"
            styles={
                size === 'small'
                    ? { root: { fontSize: 12, lineHeight: '18px', marginInlineEnd: 0 } }
                    : { root: { marginInlineEnd: 0 } }
            }
        >
            {text}
        </Tag>
    );
}
