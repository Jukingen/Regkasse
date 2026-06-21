/**
 * User activity timeline – enterprise UX.
 * GET api/AuditLog/user/{userId} with server-side pagination and date range.
 * Actor: actorDisplayName with fallback to userId. Diff viewer modal for structured changes.
 * Invariant 4: Never render sensitive fields (diff uses whitelist only).
 * Invariant 5: Gracefully handle incomplete historical records (null actor, missing timestamp/action/description;
 * use EMPTY_PLACEHOLDER and safe fallbacks so UI never crashes on legacy or partial data).
 */
import React, { useState, useMemo, useCallback } from 'react';
import { Table, Tag, Typography, Empty, Alert, Button, Select, DatePicker, Space } from 'antd';
import type { Dayjs } from 'dayjs';
import { useGetApiAuditLogUserUserId } from '@/api/generated/audit-log/audit-log';
import type { AuditLog as AuditLogType } from '@/api/generated/model/auditLog';
import dayjs from 'dayjs';
import { formatAuditLogDescription } from '@/features/audit-logs/utils/formatAuditLogDescription';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { getDiffRowsFromEntry, EMPTY_PLACEHOLDER } from '../utils/auditDiffUtils';
import { AuditDiffViewerModal } from './AuditDiffViewerModal';

const { Text } = Typography;

type Props = {
    userId: string;
    userName?: string;
};

const AUDIT_LOG_STALE_MS = 60 * 1000;
const PAGE_SIZE = 10;

type ActionFilter = 'all' | 'role' | 'updates' | 'security';

const SECURITY_ACTIONS = new Set([
    'USER_DEACTIVATE',
    'USER_REACTIVATE',
    'FORCE_RESET_PASSWORD',
    'USER_PASSWORD_RESET',
    'CHANGE_OWN_PASSWORD',
]);

function matchesActionFilter(action: string | null | undefined, filter: ActionFilter): boolean {
    const a = (action ?? '').trim();
    if (filter === 'all') return true;
    if (filter === 'role') return a === 'USER_ROLE_CHANGE';
    if (filter === 'updates') return a === 'USER_UPDATE' || a === 'USER_NAME_CHANGE';
    if (filter === 'security') return SECURITY_ACTIONS.has(a);
    return true;
}

/** Audit diff field labels for editable user fields. */
function buildAuditFieldLabels(t: (key: string) => string): Record<string, string> {
    const labels: Record<string, string> = {
        firstName: t('users.create.firstName'),
        lastName: t('users.create.lastName'),
        email: t('users.create.email'),
        userName: t('users.form.userName'),
        role: t('users.list.columnRole'),
        isActive: t('users.list.columnStatus'),
        isDemo: t('users.activity.fieldLabels.isDemo'),
        taxNumber: t('users.form.taxNumber'),
        notes: t('users.form.notes'),
        employeeNumber: t('users.form.employeeNumber'),
    };
    return {
        ...labels,
        FirstName: labels.firstName,
        LastName: labels.lastName,
        Email: labels.email,
        UserName: labels.userName,
        Role: labels.role,
        IsActive: labels.isActive,
        IsDemo: labels.isDemo,
        TaxNumber: labels.taxNumber,
        Notes: labels.notes,
        EmployeeNumber: labels.employeeNumber,
    };
}

function getActorDisplay(record: AuditEntry): string {
    const name = (record.actorDisplayName != null && String(record.actorDisplayName).trim())
        ? String(record.actorDisplayName).trim()
        : null;
    const uid = (record.userId != null && String(record.userId).trim())
        ? String(record.userId).trim()
        : null;
    return name ?? uid ?? EMPTY_PLACEHOLDER;
}

/** Entry may include extended API fields (actorDisplayName, changes). */
type AuditEntry = AuditLogType & {
    actorDisplayName?: string | null;
    changes?: string | null;
};

function hasDiff(entry: AuditEntry): boolean {
    const c = entry.changes ?? '';
    const o = entry.oldValues ?? '';
    const n = entry.newValues ?? '';
    return (typeof c === 'string' && c.trim().length > 0) ||
        (typeof o === 'string' && o.trim().length > 0) ||
        (typeof n === 'string' && n.trim().length > 0);
}

export function UserActivityTimeline({ userId, userName }: Props) {
    const { t, formatLocale } = useI18n();
    const [page, setPage] = useState(1);
    const [diffModalEntry, setDiffModalEntry] = useState<AuditEntry | null>(null);
    const [actionFilter, setActionFilter] = useState<ActionFilter>('all');
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([null, null]);

    const auditFieldLabels = useMemo(() => buildAuditFieldLabels(t), [t]);
    const getAuditDiffLabel = useCallback(
        (key: string) => auditFieldLabels[key] ?? key,
        [auditFieldLabels],
    );
    const actionFilterOptions = useMemo(
        (): { value: ActionFilter; label: string }[] => [
            { value: 'all', label: t('users.activity.filterAll') },
            { value: 'role', label: t('users.activity.filterRoleChanges') },
            { value: 'updates', label: t('users.activity.filterUserUpdates') },
            { value: 'security', label: t('users.activity.filterSecurityActions') },
        ],
        [t],
    );

    const validUserId = (userId ?? '').trim();
    const params = useMemo(() => {
        const p: { page: number; pageSize: number; startDate?: string; endDate?: string } = {
            page,
            pageSize: PAGE_SIZE,
        };
        if (dateRange[0]) p.startDate = dateRange[0].format('YYYY-MM-DD');
        if (dateRange[1]) p.endDate = dateRange[1].format('YYYY-MM-DD');
        return p;
    }, [page, dateRange]);

    const { data, isLoading, isError, refetch } = useGetApiAuditLogUserUserId(
        validUserId,
        params,
        {
            query: {
                enabled: validUserId.length > 0,
                staleTime: AUDIT_LOG_STALE_MS,
                retry: false,
                refetchOnWindowFocus: false,
                refetchOnReconnect: false,
            },
        }
    );

    const rawList = Array.isArray(data?.auditLogs) ? (data.auditLogs as AuditEntry[]) : [];
    const filteredList = useMemo(
        () => rawList.filter((r) => matchesActionFilter(r.action, actionFilter)),
        [rawList, actionFilter]
    );
    const total = typeof data?.totalCount === 'number' ? data.totalCount : 0;

    /** Open diff modal with server-fresh entry so "Änderungen" shows after save without F5 (avoids stale cache). */
    const openDiffModalWithFreshEntry = (record: AuditEntry) => {
        refetch().then((result) => {
            const list = Array.isArray(result.data?.auditLogs) ? (result.data.auditLogs as AuditEntry[]) : [];
            const fresh = list.find((e) => e.id === record.id);
            setDiffModalEntry(fresh ?? record);
        }).catch(() => setDiffModalEntry(record));
    };

    const columns = [
        {
            title: t('users.activity.time'),
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 160,
            render: (v: string | null | undefined) =>
                v && String(v).trim()
                    ? formatDateTime(v, formatLocale, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
                    : EMPTY_PLACEHOLDER,
        },
        {
            title: t('users.activity.actor'),
            key: 'actor',
            width: 160,
            ellipsis: true,
            render: (_: unknown, record: AuditEntry) => getActorDisplay(record),
        },
        {
            title: t('users.activity.ipAddress'),
            dataIndex: 'ipAddress',
            key: 'ipAddress',
            width: 110,
            ellipsis: true,
            render: (v: string | null | undefined) =>
                v != null && String(v).trim() ? String(v).trim() : EMPTY_PLACEHOLDER,
        },
        {
            title: t('users.activity.action'),
            dataIndex: 'action',
            key: 'action',
            width: 150,
            ellipsis: true,
            render: (action: string | null | undefined) => {
                const raw = action != null && String(action).trim() ? String(action).trim() : '';
                if (!raw) return <Tag color="blue">{EMPTY_PLACEHOLDER}</Tag>;
                const labelKey = getAuditActionLabelKey(raw);
                const label = labelKey ? t(labelKey as 'common.auditLogs.actionLabels.login') : raw;
                return <Tag color="blue">{label}</Tag>;
            },
        },
        {
            title: t('users.activity.description'),
            dataIndex: 'description',
            key: 'description',
            width: 240,
            ellipsis: true,
            render: (_: unknown, record: AuditEntry) => {
                const text =
                    formatAuditLogDescription(record, t) ||
                    (record.description != null && String(record.description).trim()
                        ? String(record.description).trim()
                        : '');
                return text || EMPTY_PLACEHOLDER;
            },
        },
        {
            title: t('users.list.columnStatus'),
            dataIndex: 'status',
            key: 'status',
            width: 90,
            render: (status: string | number | null | undefined) => {
                const s = status != null ? String(status) : '';
                const isSuccess = s === 'Success' || s === '0';
                return <Tag color={isSuccess ? 'green' : 'default'}>{s || EMPTY_PLACEHOLDER}</Tag>;
            },
        },
        {
            title: '',
            key: 'viewChanges',
            width: 150,
            fixed: 'right' as const,
            render: (_: unknown, record: AuditEntry) =>
                hasDiff(record) ? (
                    <Button
                        type="link"
                        size="small"
                        onClick={() => openDiffModalWithFreshEntry(record)}
                    >
                        {t('users.activity.viewChanges')}
                    </Button>
                ) : null,
        },
    ];

    if (validUserId.length === 0) {
        return <Alert type="info" title={t('users.activity.empty')} showIcon />;
    }

    if (isError) {
        return (
            <Alert
                type="warning"
                title={t('users.activity.errorLoad')}
                description={t('users.activity.errorLoadHint')}
                action={
                    <Button size="small" onClick={() => refetch()}>
                        {t('users.list.retry')}
                    </Button>
                }
            />
        );
    }

    return (
        <div>
            {userName && (
                <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                    {t('users.activity.forUser')}: {userName}
                </Text>
            )}
            <Space wrap style={{ marginBottom: 12 }} size="middle">
                <Space>
                    <Text type="secondary">{t('users.activity.filterActionType')}:</Text>
                    <Select
                        value={actionFilter}
                        onChange={setActionFilter}
                        options={actionFilterOptions}
                        style={{ minWidth: 200 }}
                    />
                </Space>
                <Space>
                    <Text type="secondary">{t('users.activity.filterDateRange')}:</Text>
                    <DatePicker.RangePicker
                        value={dateRange[0] && dateRange[1] ? dateRange : null}
                        onChange={(dates) => setDateRange(dates ? [dates[0] ?? null, dates[1] ?? null] : [null, null])}
                        format="DD.MM.YYYY"
                    />
                    {(dateRange[0] || dateRange[1]) && (
                        <Button size="small" onClick={() => setDateRange([null, null])}>
                            {t('users.activity.filterReset')}
                        </Button>
                    )}
                </Space>
            </Space>
            <Table
                size="small"
                columns={columns}
                dataSource={filteredList}
                loading={isLoading}
                rowKey={(r) => r.id ?? `${r.timestamp}-${r.action}`}
                scroll={{ x: 1060 }}
                pagination={{
                    current: page,
                    pageSize: PAGE_SIZE,
                    total,
                    showSizeChanger: false,
                    onChange: setPage,
                }}
                locale={{
                    emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('users.activity.empty')} />,
                }}
            />
            <AuditDiffViewerModal
                open={!!diffModalEntry}
                onClose={() => setDiffModalEntry(null)}
                entry={diffModalEntry}
                getLabel={getAuditDiffLabel}
                formatOptions={{
                    emptyPlaceholder: EMPTY_PLACEHOLDER,
                    labelActive: t('users.list.statusActive'),
                    labelInactive: t('users.list.statusInactive'),
                }}
            />
        </div>
    );
}
