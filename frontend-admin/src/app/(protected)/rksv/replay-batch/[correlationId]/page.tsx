'use client';

import React from 'react';
import { Card, Table, Tag, Typography, Space, Alert } from 'antd';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getReplayBatchDetail, type ReplayBatchDetailResponse, type ReplayBatchPaymentItemDto } from '@/api/replay-batch';
import dayjs from 'dayjs';

/**
 * Replay-Batch-Detail: Item-Anzahl, Success/Fail/Duplicate, Log-Trace-Link, Payment-/Receipt-Links.
 */
export default function ReplayBatchDetailPage() {
    const params = useParams();
    const correlationId = params?.correlationId as string;

    const { data, isLoading, error } = useQuery({
        queryKey: ['replay-batch', correlationId],
        queryFn: () => getReplayBatchDetail(correlationId),
        enabled: !!correlationId,
    });

    // Log-Trace: Verifications-Seite mit Correlation-ID (Audit-Logs für diese Batch).
    const auditTraceHref = data?.auditCorrelationId
        ? `/rksv/verifications?correlationId=${encodeURIComponent(data.auditCorrelationId)}`
        : null;

    const columns = [
        {
            title: 'Offline-Transaktion',
            dataIndex: 'offlineTransactionId',
            key: 'offlineTransactionId',
            render: (id: string | null) => id ? <Typography.Text code copyable>{id}</Typography.Text> : '—',
        },
        {
            title: 'Zahlung',
            key: 'paymentId',
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => (
                <Link href={`/payments?paymentId=${r.paymentId}`} target="_blank" rel="noopener noreferrer">
                    <Typography.Text code>{r.paymentId}</Typography.Text>
                </Link>
            ),
        },
        {
            title: 'Beleg',
            key: 'receipt',
            render: (_: unknown, r: ReplayBatchPaymentItemDto) =>
                r.receiptId ? (
                    <Link href={`/receipts/${r.receiptId}`} target="_blank" rel="noopener noreferrer">
                        {r.receiptNumber ?? r.receiptId}
                    </Link>
                ) : (
                    <Typography.Text type="secondary">—</Typography.Text>
                ),
        },
        {
            title: 'Betrag',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            render: (v: number) => (v != null ? `€ ${Number(v).toFixed(2)}` : '—'),
        },
        {
            title: 'Erstellt (UTC)',
            dataIndex: 'createdAtUtc',
            key: 'createdAtUtc',
            render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
        },
    ];

    return (
        <>
            <AdminPageHeader
                title={`Replay-Batch: ${correlationId ?? ''}`}
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Replay-Batch', href: '/rksv/replay-batch' },
                    { title: correlationId ?? 'Detail' },
                ]}
            />

            {error && (
                <Alert
                    type="error"
                    message="Batch konnte nicht geladen werden"
                    description={error instanceof Error ? error.message : 'Unbekannter Fehler'}
                    style={{ marginBottom: 16 }}
                />
            )}

            {data && (
                <>
                    <Card size="small" style={{ marginBottom: 16 }}>
                        <Space direction="vertical" size="small">
                            <Typography.Text strong>Zusammenfassung</Typography.Text>
                            <Space wrap>
                                <Tag color="blue">Items gesamt: {data.totalItems}</Tag>
                                <Tag color="green">Success: {data.successCount}</Tag>
                                <Tag color="orange">Failed/Duplicate: {data.failedOrDuplicateCount}</Tag>
                            </Space>
                            <Typography.Text type="secondary">
                                Correlation-ID: <Typography.Text code>{data.correlationId}</Typography.Text>
                            </Typography.Text>
                            {auditTraceHref && (
                                <Space>
                                    <Typography.Text strong>Log-Trace:</Typography.Text>
                                    <Link href={auditTraceHref}>
                                        Audit-Logs für diese Batch anzeigen
                                    </Link>
                                </Space>
                            )}
                        </Space>
                    </Card>

                    <Card title="Zahlungen / Belege in dieser Batch" size="small">
                        <Table
                            columns={columns}
                            dataSource={data.payments}
                            rowKey={(r) => r.paymentId}
                            loading={isLoading}
                            pagination={false}
                            size="small"
                        />
                    </Card>
                </>
            )}

            {!data && !error && isLoading && (
                <Card>
                    <Typography.Text type="secondary">Lade Batch-Details…</Typography.Text>
                </Card>
            )}

            {!data && !error && !isLoading && correlationId && (
                <Card>
                    <Typography.Text type="secondary">Keine Daten für diese Correlation-ID.</Typography.Text>
                </Card>
            )}
        </>
    );
}
