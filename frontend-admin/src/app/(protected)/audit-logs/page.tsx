'use client';

import React, { useState, useCallback, useEffect, useMemo } from 'react';
import { Table, Card, Typography, Tag, Space, Button, Select, DatePicker, message, Alert, Empty, Flex, Tooltip } from 'antd';
import { ClearOutlined, ReloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model';
import { AXIOS_INSTANCE } from '@/lib/axios';
import dayjs from 'dayjs';
import { viewAuditLogStatusPresentation } from '@/shared/verificationsAuditView';

const { Title } = Typography;
const { RangePicker } = DatePicker;

function getAuditListErrorMessage(error: unknown): string {
    if (error instanceof Error) return error.message;
    return 'Failed to load audit logs. Please try again.';
}

const ACTION_OPTIONS = [
    { value: 'Login', label: 'Login' },
    { value: 'CreateInvoice', label: 'Create Invoice' },
    { value: 'Payment', label: 'Payment' },
];

export default function AuditLogsPage() {
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(10);
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
    const [actionFilter, setActionFilter] = useState<string | undefined>(undefined);

    const queryParams = {
        page,
        pageSize,
        startDate: dateRange?.[0] ? dateRange[0].toISOString() : undefined,
        endDate: dateRange?.[1] ? dateRange[1].toISOString() : undefined,
        action: actionFilter,
    };

    const { data, isLoading, isError, error, refetch } = useGetApiAuditLog(queryParams);

    useEffect(() => {
        setPage(1);
    }, [dateRange, actionFilter]);

    const resetFilters = useCallback(() => {
        setDateRange(null);
        setActionFilter(undefined);
        setPage(1);
    }, []);

    const scopeSummary = useMemo(() => {
        const parts: string[] = [
            `Page ${page}`,
            `${pageSize} per page`,
            data?.totalCount != null ? `${data.totalCount.toLocaleString()} total (API)` : 'total count not yet loaded',
        ];
        if (actionFilter) parts.push(`action = ${actionFilter}`);
        if (dateRange?.[0] && dateRange[1]) {
            parts.push(
                `${dateRange[0].format('DD.MM.YYYY')}–${dateRange[1].format('DD.MM.YYYY')}`,
            );
        } else {
            parts.push('no date range');
        }
        return parts.join(' · ');
    }, [page, pageSize, data?.totalCount, actionFilter, dateRange]);

    const handleExport = useCallback(async (format: 'json' | 'csv') => {
        try {
            const params: Record<string, string> = { format };
            if (dateRange?.[0]) params.startDate = dateRange[0].toISOString();
            if (dateRange?.[1]) params.endDate = dateRange[1].toISOString();
            if (actionFilter) params.action = actionFilter;

            const res = await AXIOS_INSTANCE.get<Blob>('/api/AuditLog/export', {
                params,
                responseType: 'blob',
            });

            const blob = res.data;
            const contentType = String(res.headers?.['content-type'] ?? '').toLowerCase();

            // When backend returns an error JSON, axios still gives us a Blob (responseType='blob').
            // We must detect that case and avoid downloading "error payloads" as valid exports.
            const maybeJsonText = await blob.text();

            if (format === 'json' && contentType.includes('application/json')) {
                try {
                    const parsed = JSON.parse(maybeJsonText);
                    if (Array.isArray(parsed)) {
                        const url = URL.createObjectURL(blob as Blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = `audit_logs_${dayjs().format('YYYYMMDD_HHmmss')}.${format}`;
                        a.click();
                        URL.revokeObjectURL(url);
                        message.success('Export started');
                        return;
                    }

                    // Object-shaped JSON: treat as an error envelope.
                    const msg =
                        typeof parsed?.message === 'string'
                            ? parsed.message
                            : 'Export failed';
                    message.error(msg);
                    return;
                } catch {
                    // Not valid JSON: fall through and attempt download.
                }
            }

            if (format === 'csv' && !contentType.includes('text/csv')) {
                try {
                    const parsed = JSON.parse(maybeJsonText);
                    const msg =
                        typeof parsed?.message === 'string'
                            ? parsed.message
                            : 'Export failed';
                    message.error(msg);
                    return;
                } catch {
                    message.error('Export failed');
                    return;
                }
            }

            // Expected success formats.
            if (format === 'csv' && !contentType.includes('text/csv')) {
                message.error('Export failed');
                return;
            }

            const url = URL.createObjectURL(blob as Blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `audit_logs_${dayjs().format('YYYYMMDD_HHmmss')}.${format}`;
            a.click();
            URL.revokeObjectURL(url);
            message.success('Export started');
        } catch (e) {
            message.error('Export failed');
        }
    }, [dateRange, actionFilter]);

    const columns = [
        {
            title: 'Time',
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 168,
            render: (ts: string | undefined) => (
                <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {ts ? dayjs(ts).format('DD.MM.YYYY HH:mm:ss') : '—'}
                </Typography.Text>
            ),
        },
        {
            title: 'User',
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
            title: 'Action',
            dataIndex: 'action',
            key: 'action',
            width: 200,
            ellipsis: true,
            render: (action: string | null | undefined) => <Tag color="blue">{action ?? '—'}</Tag>,
        },
        {
            title: 'Entity',
            key: 'entity',
            width: 200,
            render: (_: unknown, record: AuditLogEntryDto) => {
                const type = record.entityType?.trim() || '—';
                const id = record.entityId?.trim();
                if (!id) {
                    return <Typography.Text strong>{type}</Typography.Text>;
                }
                return (
                    <Tooltip title={`${type} · ${id}`}>
                        <Space direction="vertical" size={0} style={{ maxWidth: 220 }}>
                            <Typography.Text strong ellipsis style={{ display: 'block' }}>
                                {type}
                            </Typography.Text>
                            <Typography.Text
                                type="secondary"
                                ellipsis
                                copyable={{ text: id }}
                                style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
                            >
                                {id}
                            </Typography.Text>
                        </Space>
                    </Tooltip>
                );
            },
        },
        {
            title: 'Details',
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
            render: (text: string | null | undefined) => {
                const t = text?.trim();
                if (!t) return <Typography.Text type="secondary">—</Typography.Text>;
                return (
                    <Tooltip title={t}>
                        <Typography.Text ellipsis style={{ maxWidth: 320, display: 'block' }}>
                            {t}
                        </Typography.Text>
                    </Tooltip>
                );
            },
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            width: 120,
            align: 'center' as const,
            render: (_: unknown, record: AuditLogEntryDto) => {
                const p = viewAuditLogStatusPresentation(record.status);
                return <Tag color={p.antColor}>{p.label}</Tag>;
            },
        },
    ];

    const rows = data?.auditLogs ?? [];
    const emptyList = !isLoading && !isError && rows.length === 0;

    return (
        <Card>
            <Flex justify="space-between" align="flex-start" wrap="wrap" gap="middle" style={{ marginBottom: 16 }}>
                <div>
                    <Title level={3} style={{ margin: 0 }}>
                        Audit Logs
                    </Title>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 8, maxWidth: 560 }}>
                        Filter by action and date range; export uses the same filters. Table shows the current API page
                        only — use pagination to scan further.
                    </Typography.Paragraph>
                </div>
                <Space wrap>
                    <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isLoading}>
                        Refresh
                    </Button>
                    <Button onClick={() => handleExport('json')} disabled={isLoading}>
                        Export JSON
                    </Button>
                    <Button onClick={() => handleExport('csv')} disabled={isLoading}>
                        Export CSV
                    </Button>
                </Space>
            </Flex>

            <Flex wrap="wrap" gap="small" align="center" style={{ marginBottom: 12 }}>
                <Typography.Text type="secondary" style={{ marginRight: 8 }}>
                    Filters
                </Typography.Text>
                <Select
                    placeholder="Action"
                    style={{ width: 168 }}
                    allowClear
                    value={actionFilter}
                    onChange={setActionFilter}
                    options={ACTION_OPTIONS}
                />
                <RangePicker
                    value={dateRange ?? undefined}
                    onChange={(dates) => setDateRange(dates as [Dayjs | null, Dayjs | null] | null)}
                    format="DD.MM.YYYY"
                />
                <Button icon={<ClearOutlined />} onClick={resetFilters}>
                    Reset filters
                </Button>
            </Flex>

            <Typography.Paragraph
                type="secondary"
                style={{ marginBottom: 12, fontSize: 12, padding: '8px 12px', background: 'var(--ant-color-fill-quaternary)', borderRadius: 6 }}
            >
                <Typography.Text strong style={{ fontSize: 12 }}>
                    Active scope:{' '}
                </Typography.Text>
                {scopeSummary}
            </Typography.Paragraph>

            {isError ? (
                <Alert
                    type="error"
                    message="Failed to load audit logs"
                    description={getAuditListErrorMessage(error)}
                    showIcon
                    style={{ marginBottom: 16 }}
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            Try again
                        </Button>
                    }
                />
            ) : null}

            {!isError ? (
                <Table<AuditLogEntryDto>
                    columns={columns}
                    dataSource={rows}
                    loading={isLoading}
                    rowKey={(r) => r.id ?? `${r.timestamp ?? ''}-${r.action ?? ''}`}
                    size="middle"
                    scroll={{ x: 1100 }}
                    pagination={{
                        current: page,
                        pageSize,
                        total: data?.totalCount ?? 0,
                        showSizeChanger: true,
                        pageSizeOptions: ['10', '25', '50', '100'],
                        showTotal: (total, range) => `${range[0]}–${range[1]} of ${total} entries`,
                        hideOnSinglePage: false,
                        onChange: (p, s) => {
                            setPage(p);
                            setPageSize(s ?? pageSize);
                        },
                    }}
                    locale={{
                        emptyText: emptyList ? (
                            <Empty
                                description="No audit entries for this query. Widen the date range or clear filters."
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                            />
                        ) : (
                            <Empty description="No data" image={Empty.PRESENTED_IMAGE_SIMPLE} />
                        ),
                    }}
                />
            ) : null}
        </Card>
    );
}
