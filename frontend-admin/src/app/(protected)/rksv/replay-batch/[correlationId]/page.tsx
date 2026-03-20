'use client';

import React from 'react';
import { Card, Table, Tag, Typography, Space, Alert, Row, Col, Statistic } from 'antd';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getApiAdminReplayBatchCorrelationId } from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { ReplayBatchPaymentItemDto } from '@/api/generated/model';
import dayjs from 'dayjs';

/**
 * Replay-Batch-Detail: item counts + observability counters + trace links + payment/receipt links.
 */
export default function ReplayBatchDetailPage() {
    const params = useParams();
    const correlationId = params?.correlationId as string;

    const { data, isLoading, error } = useQuery({
        queryKey: rksvAdminQueryKeys.replayBatch(correlationId),
        queryFn: () => getApiAdminReplayBatchCorrelationId(correlationId),
        enabled: !!correlationId,
    });

    // Log-Trace: Verifications-Seite mit Correlation-ID (Audit-Logs für diese Batch).
    const auditTraceHref = data?.auditCorrelationId
        ? `/rksv/verifications?correlationId=${encodeURIComponent(data.auditCorrelationId)}`
        : null;
    const incidentHref = data?.correlationId
        ? `/rksv/incident?correlationId=${encodeURIComponent(String(data.correlationId))}`
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
                            <Typography.Text type="secondary">
                                Audit-Correlation-ID: <Typography.Text code>{data.auditCorrelationId ?? '—'}</Typography.Text>
                            </Typography.Text>
                            {auditTraceHref && (
                                <Space wrap>
                                    <Typography.Text strong>Log-Trace:</Typography.Text>
                                    <Link href={auditTraceHref}>
                                        Audit-Logs für diese Batch anzeigen
                                    </Link>
                                    {incidentHref && (
                                        <>
                                            <Typography.Text type="secondary">|</Typography.Text>
                                            <Link href={incidentHref}>
                                                Incident-Ansicht öffnen
                                            </Link>
                                        </>
                                    )}
                                </Space>
                            )}
                        </Space>
                    </Card>

                    <Card size="small" title="Verknüpfte Entitäten" style={{ marginBottom: 16 }}>
                        <Space wrap>
                            {data.correlationId ? (
                                <Link href={`/rksv/incident?correlationId=${encodeURIComponent(String(data.correlationId))}`}>
                                    Incident (Correlation)
                                </Link>
                            ) : null}
                            {auditTraceHref ? (
                                <>
                                    <Typography.Text type="secondary">·</Typography.Text>
                                    <Link href={auditTraceHref}>Verifications (Audit)</Link>
                                </>
                            ) : null}
                        </Space>
                    </Card>

                    <Card size="small" title="Observability" style={{ marginBottom: 16 }}>
                        <Row gutter={[12, 12]}>
                            <Col xs={24} sm={8}>
                                <Card size="small">
                                    <Statistic
                                        title="Coverage-Samples"
                                        value={data.coverageSampleCount ?? 0}
                                    />
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        Replay-Loop Samples (Device/Sequence Coverage)
                                    </Typography.Text>
                                </Card>
                            </Col>
                            <Col xs={24} sm={8}>
                                <Card size="small">
                                    <Statistic
                                        title="Audit OFFLINE_SYNCED"
                                        value={data.offlineSyncedAuditCount ?? 0}
                                    />
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        Immutable Success-Audit Events
                                    </Typography.Text>
                                </Card>
                            </Col>
                            <Col xs={24} sm={8}>
                                <Card size="small">
                                    <Statistic
                                        title="Audit FINAL_FAILURE"
                                        value={data.offlineFinalFailureAuditCount ?? 0}
                                        valueStyle={{
                                            color: (data.offlineFinalFailureAuditCount ?? 0) > 0 ? '#cf1322' : undefined,
                                        }}
                                    />
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        Final Failure Events (terminal replay)
                                    </Typography.Text>
                                </Card>
                            </Col>
                        </Row>
                    </Card>

                    <Card title="Zahlungen / Belege in dieser Batch" size="small">
                        <Table
                            columns={columns}
                            dataSource={data.payments ?? []}
                            rowKey={(r) => r.paymentId ?? r.offlineTransactionId ?? r.receiptId ?? 'unknown'}
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
