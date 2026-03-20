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
import { ReloadOutlined, SyncOutlined } from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import dayjs, { type Dayjs } from 'dayjs';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
    getApiAdminFinanzonlineReconciliation,
    getApiAdminFinanzonlineReconciliationMetrics,
    postApiAdminFinanzonlineReconciliationRetryPaymentId,
} from '@/api/generated/admin/admin';
import {
    getAdminCashRegisters,
} from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type {
    FinanzOnlineReconciliationItemDto,
    GetApiAdminFinanzonlineReconciliationParams,
} from '@/api/generated/model';

const STATUS_OPTIONS: { value: string; label: string }[] = [
    { value: 'Pending', label: 'Pending' },
    { value: 'Failed', label: 'Failed' },
    { value: 'NeedsReconciliation', label: 'NeedsReconciliation' },
    { value: 'Submitted', label: 'Submitted' },
];

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
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();
    const initialStatusFilter = useMemo(() => {
        const raw = searchParams?.get('status');
        if (!raw) return ['Pending', 'Failed', 'NeedsReconciliation'];
        return raw
            .split(',')
            .map((x) => x.trim())
            .filter((x) => x.length > 0);
    }, [searchParams]);
    const initialCashRegisterId = useMemo(() => {
        const raw = searchParams?.get('cashRegisterId');
        return raw || undefined;
    }, [searchParams]);
    const initialDateRange = useMemo<[Dayjs | null, Dayjs | null]>(() => {
        const from = searchParams?.get('fromUtc');
        const to = searchParams?.get('toUtc');
        const fromDayjs = from && dayjs(from).isValid() ? dayjs(from) : null;
        const toDayjs = to && dayjs(to).isValid() ? dayjs(to) : null;
        return [fromDayjs, toDayjs];
    }, [searchParams]);

    const [statusFilter, setStatusFilter] = useState<string[]>(initialStatusFilter);
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(initialCashRegisterId);
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>(initialDateRange);
    const [retryingId, setRetryingId] = useState<string | null>(null);

    const listParams: GetApiAdminFinanzonlineReconciliationParams = useMemo(() => {
        const p: GetApiAdminFinanzonlineReconciliationParams = {
            status: statusFilter.length ? statusFilter.join(',') : undefined,
            limit: 200,
        };
        if (cashRegisterId) p.cashRegisterId = cashRegisterId;
        if (dateRange[0]) p.fromUtc = dateRange[0].toISOString();
        if (dateRange[1]) p.toUtc = dateRange[1].endOf('day').toISOString();
        return p;
    }, [statusFilter, cashRegisterId, dateRange]);

    const { data: listData, isLoading: listLoading, error: listError } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnline.list(listParams),
        queryFn: () => getApiAdminFinanzonlineReconciliation(listParams),
        staleTime: 30_000,
    });

    const { data: metricsData, isLoading: metricsLoading } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnline.metrics,
        queryFn: getApiAdminFinanzonlineReconciliationMetrics,
        staleTime: 15_000,
    });

    const { data: cashRegisters } = useQuery({
        queryKey: rksvAdminQueryKeys.cashRegisters,
        queryFn: getAdminCashRegisters,
        staleTime: 60_000,
    });

    const retryMutation = useMutation({
        mutationFn: (paymentId: string) => postApiAdminFinanzonlineReconciliationRetryPaymentId(paymentId),
        onSuccess: (result, paymentId) => {
            if (result.success) {
                message.success(`Zahlung ${paymentId}: ${result.message}`);
            } else {
                message.warning(`Zahlung ${paymentId}: ${result.message}`);
            }
            setRetryingId(null);
            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.metrics });
        },
        onError: (err: Error, paymentId) => {
            const msg = err?.message || 'Retry fehlgeschlagen';
            message.error(`Retry für ${paymentId}: ${msg}`);
            setRetryingId(null);
            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
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
            render: (val: string) => (
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
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const paymentId = r.paymentId ?? '';
                return (
                    <Link href={`/payments?paymentId=${paymentId}`} target="_blank" rel="noopener noreferrer">
                        <Typography.Text code>{paymentId ? `${paymentId.slice(0, 8)}…` : '—'}</Typography.Text>
                    </Link>
                );
            },
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
                const paymentId = r.paymentId ?? '';
                const loading = retryingId === paymentId;
                return canRetry && paymentId ? (
                    <Button
                        type="link"
                        size="small"
                        icon={<SyncOutlined />}
                        loading={loading}
                        onClick={() => handleRetry(paymentId)}
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
                            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
                            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.metrics });
                        }}
                    >
                        Aktualisieren
                    </Button>
                }
            />

            <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                Verwandt:{' '}
                <Link href="/rksv/finanz-online-operations">FinanzOnline Operations</Link>
                {' · '}
                <Link href="/rksv/integrity">Datenintegrität (Support)</Link>
                {' · '}
                <Link href="/rksv/incident">Incident (Correlation)</Link>
                {' · '}
                <Link href="/payments">Payments</Link>
            </Typography.Paragraph>

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
                            options={(cashRegisters ?? [])
                                .filter((r) => typeof r.id === 'string' && r.id.length > 0)
                                .map((r) => ({
                                    value: r.id as string,
                                    label: r.registerNumber ? `${r.registerNumber} (${(r.id as string).slice(0, 8)}…)` : (r.id as string),
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
                        rowKey={(row) => row.paymentId ?? `${row.receiptNumber}-${row.createdAt}`}
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
