'use client';

import { useMemo } from 'react';
import { Empty, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { formatDateTime, formatNumber } from '@/i18n/formatting';

import type { AuditLogEntryDto } from '@/api/generated/model';
import { AuditLogDetailsCell } from '@/features/audit-logs/components/AuditLogDetailsCell';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { formatAuditLogReason } from '@/features/audit-logs/utils/formatAuditLogDescription';
import { viewAuditLogStatusPresentation } from '@/shared/verificationsAuditView';
import { useI18n } from '@/i18n';

export type AuditLogTableProps = {
    rows: AuditLogEntryDto[];
    loading: boolean;
    page: number;
    pageSize: number;
    total: number;
    hasActiveFilters: boolean;
    onPageChange: (page: number, pageSize: number) => void;
    onRowClick?: (record: AuditLogEntryDto) => void;
    /** When true, keep showing prior rows while the next page loads (no full-table spinner). */
    isPlaceholderData?: boolean;
    /** Server hint when total count was not computed for this page. */
    hasMore?: boolean;
};

export function AuditLogTable({
    rows,
    loading,
    page,
    pageSize,
    total,
    hasActiveFilters,
    onPageChange,
    onRowClick,
    isPlaceholderData = false,
    hasMore = false,
}: AuditLogTableProps) {
    const { t, formatLocale } = useI18n();

    const columns = useMemo(
        () => [
            {
                title: t('common.auditLogs.table.time'),
                dataIndex: 'timestamp',
                key: 'timestamp',
                width: 168,
                render: (ts: string | undefined) => (
                    <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {ts
                            ? formatDateTime(ts, '', {
                                  hour: '2-digit',
                                  minute: '2-digit',
                                  second: '2-digit',
                              })
                            : '—'}
                    </Typography.Text>
                ),
            },
            {
                title: t('common.auditLogs.table.correlation'),
                key: 'correlationId',
                width: 112,
                ellipsis: true,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const c = record.correlationId?.trim();
                    if (!c) return <Typography.Text type="secondary">—</Typography.Text>;
                    return (
                        <Typography.Text code copyable={{ text: c }} ellipsis style={{ fontSize: 11, maxWidth: 104 }}>
                            {c.length > 14 ? `${c.slice(0, 12)}…` : c}
                        </Typography.Text>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.user'),
                key: 'userName',
                width: 140,
                ellipsis: true,
                render: (_: unknown, record: AuditLogEntryDto) => (
                    <Typography.Text type="secondary" ellipsis={{ tooltip: true }}>
                        {record.actorDisplayName ?? record.createdBy ?? record.userId ?? '—'}
                    </Typography.Text>
                ),
            },
            {
                title: t('common.auditLogs.table.action'),
                dataIndex: 'action',
                key: 'action',
                width: 200,
                ellipsis: true,
                render: (action: string | null | undefined) => {
                    const raw = action?.trim();
                    if (!raw) return <Tag color="blue">—</Tag>;
                    const labelKey = getAuditActionLabelKey(raw);
                    const label = labelKey ? t(labelKey as 'common.auditLogs.actionLabels.login') : raw;
                    return (
                        <Tooltip title={raw}>
                            <Tag color="blue">{label}</Tag>
                        </Tooltip>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.entity'),
                key: 'entity',
                width: 200,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const type = record.entityType?.trim() || '—';
                    const id = record.entityId?.trim();
                    if (!id) {
                        return <Typography.Text strong>{type}</Typography.Text>;
                    }
                    return (
                        <Space orientation="vertical" size={0} style={{ maxWidth: 220 }}>
                            <Typography.Text strong ellipsis style={{ display: 'block' }}>
                                {type}
                            </Typography.Text>
                            <Typography.Text
                                type="secondary"
                                ellipsis={{ tooltip: true }}
                                copyable={{ text: id }}
                                style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
                            >
                                {id}
                            </Typography.Text>
                        </Space>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.details'),
                key: 'description',
                ellipsis: true,
                render: (_: unknown, record: AuditLogEntryDto) => (
                    <AuditLogDetailsCell record={record} translate={t} />
                ),
            },
            {
                title: t('common.auditLogs.table.reason'),
                key: 'reason',
                width: 160,
                ellipsis: true,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const reason = formatAuditLogReason(record);
                    if (!reason) return <Typography.Text type="secondary">—</Typography.Text>;
                    return (
                        <Tooltip title={reason}>
                            <Typography.Text ellipsis style={{ maxWidth: 150, display: 'block' }}>
                                {reason}
                            </Typography.Text>
                        </Tooltip>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.status'),
                dataIndex: 'status',
                key: 'status',
                width: 120,
                align: 'center' as const,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const p = viewAuditLogStatusPresentation(record.status);
                    return <Tag color={p.antColor}>{p.label}</Tag>;
                },
            },
        ],
        [t],
    );

    const emptyList = !loading && rows.length === 0;

    return (
        <Table<AuditLogEntryDto>
            columns={columns}
            dataSource={rows}
            loading={loading && !isPlaceholderData}
            virtual={shouldUseAdminTableVirtual(rows.length)}
            rowKey={(r) => r.id ?? `${r.timestamp ?? ''}-${r.action ?? ''}`}
            onRow={
                onRowClick
                    ? (record) => ({
                          onClick: () => onRowClick(record),
                          style: { cursor: 'pointer' },
                      })
                    : undefined
            }
            size="middle"
            scroll={adminTableScrollXy(1400, rows.length)}
            style={{
                opacity: isPlaceholderData ? 0.6 : 1,
                transition: 'opacity 0.2s',
            }}
            pagination={{
                current: page,
                pageSize,
                total,
                showSizeChanger: true,
                pageSizeOptions: ['10', '25', '50', '100'],
                showTotal: (totalCount, range) => {
                    if (totalCount <= 0) return t('common.auditLogs.paginationZero');
                    return t('common.auditLogs.paginationRangeOfTotal', {
                        from: String(range[0] ?? 0),
                        to: String(range[1] ?? 0),
                        total: formatNumber(totalCount, formatLocale, { maximumFractionDigits: 0 }),
                    });
                },
                hideOnSinglePage: false,
                showQuickJumper: false,
                onChange: (p, s) => onPageChange(p, s ?? pageSize),
            }}
            locale={{
                emptyText: emptyList ? (
                    <Empty
                        description={
                            hasActiveFilters
                                ? t('common.auditLogs.emptyFiltered')
                                : t('common.auditLogs.emptyNoRows')
                        }
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                    />
                ) : (
                    <Empty
                        description={t('common.auditLogs.emptyNoRows')}
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                    />
                ),
            }}
        />
    );
}
