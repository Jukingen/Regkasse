'use client';

import React, { useState, useCallback } from 'react';
import { Table, Card, Typography, Tag, Space, Button, Select, DatePicker, message } from 'antd';
import type { Dayjs } from 'dayjs';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model';
import { AXIOS_INSTANCE } from '@/lib/axios';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

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

    const { data, isLoading } = useGetApiAuditLog(queryParams);

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
            title: 'Timestamp',
            dataIndex: 'timestamp',
            key: 'timestamp',
            render: (ts: string) => dayjs(ts).format('DD.MM.YYYY HH:mm:ss'),
        },
        {
            title: 'User',
            key: 'userName',
            render: (_: unknown, record: AuditLogEntryDto) =>
                record.actorDisplayName ?? record.createdBy ?? record.userId ?? '—',
        },
        {
            title: 'Action',
            dataIndex: 'action',
            key: 'action',
            render: (action: string | null | undefined) => <Tag color="blue">{action ?? '—'}</Tag>,
        },
        {
            title: 'Entity',
            dataIndex: 'entityType',
            key: 'entityType',
        },
        {
            title: 'Details',
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => (
                <Tag color={status === 'Success' ? 'green' : 'red'}>{status}</Tag>
            ),
        }
    ];

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <Title level={3} style={{ margin: 0 }}>Audit Logs</Title>
                <Space wrap>
                    <Select
                        placeholder="Action"
                        style={{ width: 160 }}
                        allowClear
                        value={actionFilter}
                        onChange={setActionFilter}
                        options={ACTION_OPTIONS}
                    />
                    <RangePicker
                        value={dateRange ?? undefined}
                        onChange={(dates) => setDateRange(dates as [Dayjs | null, Dayjs | null] | null)}
                    />
                    <Button onClick={() => handleExport('json')}>Export JSON</Button>
                    <Button onClick={() => handleExport('csv')}>Export CSV</Button>
                </Space>
            </div>

            <Table
                columns={columns}
                dataSource={data?.auditLogs ?? []}
                loading={isLoading}
                rowKey="id"
                pagination={{
                    current: page,
                    pageSize: pageSize,
                    total: data?.totalCount,
                    onChange: (p, s) => {
                        setPage(p);
                        setPageSize(s ?? pageSize);
                    },
                }}
            />
        </Card>
    );
}
