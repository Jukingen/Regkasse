'use client';

/**
 * FinanzOnline Reconciliation Console — list, filter, retry FO submission state.
 * Replaces the old queue/status screen; uses GET/POST /api/admin/finanzonline-reconciliation.
 */

import React, { useMemo, useState } from 'react';
import {
    Card,
    Table,
    Tag,
    Statistic,
    Row,
    Col,
    Spin,
    Alert,
    Button,
    Space,
    Select,
    DatePicker,
    message,
    Typography,
    Tooltip,
} from 'antd';
import { ReloadOutlined, RetryOutlined } from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import dayjs, { type Dayjs } from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
    getReconciliationList,
    getReconciliationMetrics,
    retryReconciliationSubmit,
    type FinanzOnlineReconciliationItemDto,
    type GetReconciliationListParams,
} from '@/api/finanzonline-reconciliation';
import { customInstance } from '@/lib/axios';

const STATUS_OPTIONS: { value: string; label: string }[] = [
    { value: 'Pending', label: 'Pending' },
    { value: 'Failed', label: 'Failed' },
    { value: 'NeedsReconciliation', label: 'NeedsReconciliation' },
    { value: 'Submitted', label: 'Submitted' },
];

// Cash register list response from GET /api/CashRegister
interface CashRegisterListResponse {
    registers?: { id: string; registerNumber?: string }[];
}

const RECONCILIATION_QUERY_KEY = ['admin', 'finanzonline-reconciliation'];
const METRICS_QUERY_KEY = ['admin', 'finanzonline-reconciliation', 'metrics'];

function statusBadgeColor(status: string | null): string {
    if (!status) return 'default';
    switch (status) {
        case 'Submitted':
            return 'green';
        case 'Pending':
            return 'blue';
        case 'Failed':
            return 'red';
        case 'NeedsReconciliation':
            return 'orange';
        default:
            return 'default';
    }
}

export default function FinanzOnlineReconciliationPage() {
    const queryClient = useQueryClient();
    const [statusFilter, setStatusFilter] = useState<string[]>(['Pending', 'Failed', 'NeedsReconciliation']);
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([null, null]);
    const [retryingId, setRetryingId] = useState<string | null>(null);

    const listParams: GetReconciliationListParams = useMemo(() => {
        const p: GetReconciliationListParams = {
            status: statusFilter.length ? statusFilter.join(',') : undefined,
            limit: 200,
        };
        if (cashRegisterId) p.cashRegisterId = cashRegisterId;
        if (dateRange[0]) p.fromUtc = dateRange[0].utc().toISOString();
        if (dateRange[1]) p.toUtc = dateRange[1].utc().endOf('day').toISOString();
        return p;
    }, [statusFilter, cashRegisterId, dateRange]);

    const { data: listData, isLoading: listLoading, error: listError } = useQuery({
        queryKey: [...RECONCILIATION_QUERY_KEY, listParams],
        queryFn: () => getReconciliationList(listParams),
        staleTime: 30_000,
    });

    const { data: metricsData, isLoading: metricsLoading } = useQuery({
        queryKey: METRICS_QUERY_KEY,
        queryFn: getReconciliationMetrics,
        staleTime: 15_000,
    });

    const { data: cashRegisters } = useQuery({
        queryKey: ['cash-registers'],
        queryFn: async () =>
            customInstance<CashRegisterListResponse>({ url: '/api/CashRegister', method: 'GET' }),
        staleTime: 60_000,
    });

    const retryMutation = useMutation({
        mutationFn: retryReconciliationSubmit,
        onSuccess: (result, paymentId) => {
            if (result.success) {
                message.success(`Zahlung ${paymentId}: ${result.message}`);
            } else {
                message.warning(`Zahlung ${paymentId}: ${result.message}`);
            }
            setRetryingId(null);
            queryClient.invalidateQueries({ queryKey: RECONCILIATION_QUERY_KEY });
            queryClient.invalidateQueries({ queryKey: METRICS_QUERY_KEY });
        },
        onError: (err: Error, paymentId) => {
            const msg = err?.message || 'Retry fehlgeschlagen';
            message.error(`Retry für ${paymentId}: ${msg}`);
            setRetryingId(null);
            queryClient.invalidateQueries({ queryKey: RECONCILIATION_QUERY_KEY });
        },
    });

    const handleRetry = (paymentId: string) => {
        setRetryingId(paymentId);
        retryMutation.mutate(paymentId);
    };

    const columns = [
        {
            title: 'Belegnummer',
            dataIndex: 'receiptNumber',
            key: 'receiptNumber',
            width: 160,
            render: (val: string, r: FinanzOnlineReconciliationItemDto) => (
                <Space direction="vertical" size={0}>
                    <Typography.Text code copyable>
                        {val || '—'}
                    </Typography.Text>
                    <Link href={`/receipts?receiptNumber=${encodeURIComponent(val || '')}`} target="_blank" rel="noopener noreferrer">
                        Belege suchen
                    </Link>
                </Space>
            ),
        },
        {
            title: 'Zahlung',
            key: 'paymentId',
            width: 120,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => (
                <Link href={`/payments?paymentId=${r.paymentId}`} target="_blank" rel="noopener noreferrer">
                    <Typography.Text code>{r.paymentId.slice(0, 8)}…</Typography.Text>
                </Link>
            ),
        },
        {
            title: 'Status',
            dataIndex: 'finanzOnlineStatus',
            key: 'finanzOnlineStatus',
            width: 140,
            render: (val: string | null) => (
                <Tag color={statusBadgeColor(val)}>{val ?? '—'}</Tag>
            ),
        },
        {
            title: 'Retries',
            dataIndex: 'finanzOnlineRetryCount',
            key: 'finanzOnlineRetryCount',
            width: 80,
            align: 'center' as const,
        },
        {
            title: 'Letzter Versuch (UTC)',
            dataIndex: 'finanzOnlineLastAttemptAtUtc',
            key: 'finanzOnlineLastAttemptAtUtc',
            width: 170,
            render: (v: string | null) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm') : '—'),
        },
        {
            title: 'Fehlermeldung',
            dataIndex: 'finanzOnlineError',
            key: 'finanzOnlineError',
            ellipsis: true,
            render: (v: string | null) =>
                v ? (
                    <Tooltip title={v}>
                        <Typography.Text type="danger" ellipsis>
                            {v}
                        </Typography.Text>
                    </Tooltip>
                ) : (
                    '—'
                ),
        },
        {
            title: 'Referenz (FO)',
            dataIndex: 'finanzOnlineReferenceId',
            key: 'finanzOnlineReferenceId',
            width: 120,
            ellipsis: true,
            render: (v: string | null) => (v ? <Typography.Text code copyable>{v}</Typography.Text> : '—'),
        },
        {
            title: 'Betrag',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            width: 90,
            render: (v: number) => (v != null ? `€ ${Number(v).toFixed(2)}` : '—'),
        },
        {
            title: 'Aktionen',
            key: 'actions',
            width: 100,
            fixed: 'right' as const,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const canRetry =
                    r.finanzOnlineStatus === 'Pending' ||
                    r.finanzOnlineStatus === 'Failed' ||
                    r.finanzOnlineStatus === 'NeedsReconciliation';
                const loading = retryingId === r.paymentId;
                return canRetry ? (
                    <Button
                        type="link"
                        size="small"
                        icon={<RetryOutlined />}
                        loading={loading}
                        onClick={() => handleRetry(r.paymentId)}
                    >
                        Erneut senden
                    </Button>
                ) : null;
            },
        },
    ];

    const isLoading = listLoading || metricsLoading;
    const items = listData?.items ?? [];

    return (
        <>
            <AdminPageHeader
                title="FinanzOnline Abgleich"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'FinanzOnline Abgleich' },
                ]}
                actions={
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() => {
                            queryClient.invalidateQueries({ queryKey: RECONCILIATION_QUERY_KEY });
                            queryClient.invalidateQueries({ queryKey: METRICS_QUERY_KEY });
                        }}
                    >
                        Aktualisieren
                    </Button>
                }
            />

            {listError && (
                <Alert
                    type="error"
                    message="Liste konnte nicht geladen werden"
                    description={listError instanceof Error ? listError.message : 'Unbekannter Fehler'}
                    style={{ marginBottom: 16 }}
                    showIcon
                />
            )}

            <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic
                            title="Submit gesamt (Lauf)"
                            value={metricsData?.submitTotal ?? 0}
                            loading={metricsLoading}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic
                            title="Fehlgeschlagen gesamt"
                            value={metricsData?.submitFailedTotal ?? 0}
                            loading={metricsLoading}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic
                            title="Transient"
                            value={metricsData?.submitFailedTransient ?? 0}
                            loading={metricsLoading}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic
                            title="Permanent"
                            value={metricsData?.submitFailedPermanent ?? 0}
                            loading={metricsLoading}
                        />
                    </Card>
                </Col>
            </Row>

            <Card title="Filter" size="small" style={{ marginBottom: 16 }}>
                <Space wrap size="middle">
                    <Space>
                        <Typography.Text strong>Status:</Typography.Text>
                        <Select
                            mode="multiple"
                            placeholder="Status wählen"
                            value={statusFilter}
                            onChange={(v) => setStatusFilter(v ?? [])}
                            options={STATUS_OPTIONS}
                            style={{ minWidth: 260 }}
                        />
                    </Space>
                    <Space>
                        <Typography.Text strong>Kasse:</Typography.Text>
                        <Select
                            placeholder="Alle Kassen"
                            allowClear
                            value={cashRegisterId || undefined}
                            onChange={(v) => setCashRegisterId(v ?? undefined)}
                            style={{ minWidth: 200 }}
                            options={(cashRegisters?.registers ?? []).map((r) => ({
                                value: r.id,
                                label: r.registerNumber ? `${r.registerNumber} (${r.id.slice(0, 8)}…)` : r.id,
                            }))}
                        />
                    </Space>
                    <Space>
                        <Typography.Text strong>Zeitraum (UTC):</Typography.Text>
                        <DatePicker.RangePicker
                            value={[dateRange[0], dateRange[1]]}
                            onChange={(dates) => setDateRange(dates ?? [null, null])}
                            showTime
                        />
                    </Space>
                </Space>
            </Card>

            <Card title={`Abgleich (${items.length} Einträge)`} size="small">
                {isLoading && !listData ? (
                    <div style={{ textAlign: 'center', padding: 48 }}>
                        <Spin size="large" />
                    </div>
                ) : items.length === 0 ? (
                    <Alert
                        type="info"
                        message="Keine Einträge"
                        description="Keine Zahlungen entsprechen den gewählten Filtern. Status oder Zeitraum anpassen."
                        showIcon
                    />
                ) : (
                    <Table
                        columns={columns}
                        dataSource={items}
                        rowKey="paymentId"
                        loading={listLoading}
                        pagination={{
                            pageSize: 50,
                            showSizeChanger: true,
                            showTotal: (total) => `Gesamt: ${total}`,
                        }}
                        size="small"
                        scroll={{ x: 1200 }}
                    />
                )}
            </Card>
        </>
    );
}
