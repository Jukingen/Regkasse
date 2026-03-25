'use client';

import React from 'react';
import { Card, Table, Tag, Typography, Space, Alert, Row, Col, Statistic, Descriptions, Button, Tooltip, Divider } from 'antd';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { getApiAdminReplayBatchCorrelationId } from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { ReplayBatchPaymentItemDto } from '@/api/generated/model';
import dayjs from 'dayjs';
import { viewReplayBatchTraceIds } from '@/shared/rksvAdminTruth';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildVerificationsAuditHref,
} from '@/shared/investigationNavigation';
import { AdminTruthBadge } from '@/shared/adminTruthBadges';
import {
    OPERATOR_LINK_LABELS,
    OPERATOR_REPLAY_COPY,
    OPERATOR_SHARED_COPY,
    OPERATOR_FO_SUMMARY_SCREEN_COPY,
} from '@/shared/operatorTruthCopy';

/**
 * Replay-Batch-Detail: item counts + observability counters + trace links + payment/receipt links.
 */
export default function ReplayBatchDetailPage() {
    const params = useParams();
    const correlationId = typeof params?.correlationId === 'string' ? params.correlationId : '';

    const { data, isLoading, error } = useQuery({
        queryKey: rksvAdminQueryKeys.replayBatch(correlationId),
        queryFn: () => getApiAdminReplayBatchCorrelationId(correlationId),
        enabled: !!correlationId,
    });

    const trace = data ? viewReplayBatchTraceIds(data, { verificationsAuditOnly: true }) : null;
    const auditTraceHref = trace?.verificationsDeepLink ?? null;
    const incidentHref = trace?.incidentDeepLink ?? null;

    const needsPerPaymentChecking = Boolean((data?.failedOrDuplicateCount ?? 0) > 0);

    const columns = [
        {
            title: (
                <Tooltip title="OfflineTransactionId aus ReplayBatchPaymentItemDto (für Replay-/Dedup-Analyse).">
                    <span>Offline-Transaktion</span>
                </Tooltip>
            ),
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
            title: 'Aktionen',
            key: 'actions',
            width: 180,
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const pid = r.paymentId ? String(r.paymentId) : '';
                return (
                    <Space wrap size={[8, 4]}>
                        {pid ? (
                            <Link
                                href={buildFinanzOnlineQueueInvestigationHref({
                                    focusPaymentId: pid,
                                    investigationBatchCorrelationId: correlationId || undefined,
                                })}
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                            </Link>
                        ) : (
                            <Typography.Text type="secondary">—</Typography.Text>
                        )}
                    </Space>
                );
            },
        },
        {
            title: 'Betrag',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            render: (v: number) => (v != null ? `€ ${Number(v).toFixed(2)}` : '—'),
        },
        {
            title: (
                <Tooltip title="createdAtUtc aus dem ReplayBatchPaymentItemDto (UTC).">
                    <span>Replay-Zeit (UTC)</span>
                </Tooltip>
            ),
            dataIndex: 'createdAtUtc',
            key: 'createdAtUtc',
            render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
        },
    ];

    return (
        <>
            <AdminPageHeader
                title="Replay-Batch — Detail"
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: 'Replay-Batch', href: '/rksv/replay-batch' },
                    { title: correlationId ?? 'Detail' },
                ]}
                actions={
                    correlationId ? (
                        <Typography.Text code copyable={{ text: correlationId }}>
                            {correlationId}
                        </Typography.Text>
                    ) : undefined
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 980 }}>
                    Technischer Replay-Kontext für eine Batch-Correlation (Observability + Zahlungs-/Belegverknüpfungen).
                    Diese Seite ist <strong>nicht</strong> die finale FinanzOnline-Wahrheit je Zahlung — dafür den{' '}
                    <Link href="/rksv/finanz-online-queue">{OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}</Link>{' '}
                    verwenden.
                </Typography.Paragraph>
            </AdminPageHeader>

            {error && (
                <Alert
                    type="error"
                    message={OPERATOR_SHARED_COPY.loadFailedBatch}
                    description={
                        error instanceof Error ? error.message : OPERATOR_SHARED_COPY.unknownErrorDetail
                    }
                    style={{ marginBottom: 16 }}
                />
            )}

            {data && (
                <>
                    <Card size="small" style={{ marginBottom: 16 }}>
                        <Space direction="vertical" size="small">
                            <Typography.Text strong>Batch-Überblick</Typography.Text>
                            <Row gutter={[12, 12]}>
                                <Col xs={24} sm={8}>
                                    <Card size="small">
                                        <Statistic title="Items gesamt" value={data.totalItems ?? 0} />
                                    </Card>
                                </Col>
                                <Col xs={24} sm={8}>
                                    <Card size="small">
                                        <Statistic title="Erfolgreich (Replay)" value={data.successCount ?? 0} />
                                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                            Hinweis: Erfolg hier bezieht sich auf Replay-/Batch-Verarbeitung, nicht auf FinanzOnline.
                                        </Typography.Text>
                                    </Card>
                                </Col>
                                <Col xs={24} sm={8}>
                                    <Card size="small">
                                        <Statistic
                                            title="Fehler / Duplikat"
                                            value={data.failedOrDuplicateCount ?? 0}
                                            valueStyle={{
                                                color: (data.failedOrDuplicateCount ?? 0) > 0 ? '#cf1322' : undefined,
                                            }}
                                        />
                                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                            {needsPerPaymentChecking
                                                ? 'Weiter prüfen: betroffene Zahlungen im FinanzOnline-Abgleich öffnen.'
                                                : 'Kein Replay-Fehler/Duplikat gemeldet; FO-Status trotzdem im Abgleich prüfen.'}
                                        </Typography.Text>
                                    </Card>
                                </Col>
                            </Row>

                            <Descriptions bordered size="small" column={1} style={{ marginTop: 8 }}>
                                <Descriptions.Item label="Batch-Correlation (API)">
                                    <Typography.Text code copyable>
                                        {String(data.correlationId ?? '—')}
                                    </Typography.Text>
                                </Descriptions.Item>
                                <Descriptions.Item label="Audit-Correlation (API)">
                                    <Typography.Text code copyable>
                                        {data.auditCorrelationId ?? '—'}
                                    </Typography.Text>
                                </Descriptions.Item>
                            </Descriptions>

                            <Space wrap style={{ marginTop: 8 }}>
                                <Button type="primary" href={buildIncidentInvestigationHref(String(data.correlationId))}>
                                    {OPERATOR_LINK_LABELS.incidentAggregate}
                                </Button>
                                <Button
                                    href={buildFinanzOnlineQueueInvestigationHref({
                                        investigationBatchCorrelationId: String(data.correlationId),
                                    })}
                                    target="_blank"
                                >
                                    {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                                </Button>
                                {auditTraceHref ? (
                                    <Button href={auditTraceHref}>
                                        {OPERATOR_LINK_LABELS.verificationsAudit}
                                    </Button>
                                ) : data.correlationId ? (
                                    <Button href={buildVerificationsAuditHref(String(data.correlationId))}>
                                        {OPERATOR_LINK_LABELS.verificationsAudit}
                                    </Button>
                                ) : null}
                                {incidentHref ? (
                                    <Button href={incidentHref}>
                                        Incident (Deep-Link)
                                    </Button>
                                ) : null}
                            </Space>
                        </Space>
                    </Card>

                    <Card size="small" title={OPERATOR_REPLAY_COPY.investigationPathTitle} style={{ marginBottom: 16 }}>
                        <Space direction="vertical" size={10} style={{ width: '100%' }}>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {OPERATOR_REPLAY_COPY.investigationPathIntro}
                            </Typography.Text>
                            <Space wrap align="center">
                                <AdminTruthBadge kind="authoritative_api" />
                                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                    {OPERATOR_REPLAY_COPY.batchCorrelationContextBadgeNote}
                                </Typography.Text>
                            </Space>
                            <Space
                                wrap
                                split={<Typography.Text type="secondary">·</Typography.Text>}
                            >
                                {[
                                    data.correlationId ? (
                                        <Link
                                            key="inc"
                                            href={buildIncidentInvestigationHref(String(data.correlationId))}
                                        >
                                            {OPERATOR_LINK_LABELS.incidentAggregate}
                                        </Link>
                                    ) : null,
                                    auditTraceHref ? (
                                        <Link key="ver" href={auditTraceHref}>
                                            {OPERATOR_LINK_LABELS.verificationsAudit}
                                        </Link>
                                    ) : data.correlationId ? (
                                        <Typography.Text key="verf" type="secondary" style={{ fontSize: 12 }}>
                                            {OPERATOR_REPLAY_COPY.verificationsFallbackLabel}{' '}
                                            <Typography.Text code>auditCorrelationId</Typography.Text>{' '}
                                            {OPERATOR_REPLAY_COPY.verificationsFallbackMid}{' '}
                                            <Link href={buildVerificationsAuditHref(String(data.correlationId))}>
                                                {OPERATOR_REPLAY_COPY.verificationsFallbackLinkLabel}
                                            </Link>{' '}
                                            {OPERATOR_REPLAY_COPY.verificationsFallbackAfterLink}
                                        </Typography.Text>
                                    ) : null,
                                    <Link
                                        key="fo"
                                        href={buildFinanzOnlineQueueInvestigationHref({
                                            investigationBatchCorrelationId: data.correlationId
                                                ? String(data.correlationId)
                                                : undefined,
                                        })}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        {OPERATOR_LINK_LABELS.finanzQueueContext}
                                    </Link>,
                                ].filter(Boolean)}
                            </Space>
                        </Space>
                    </Card>

                    <Card size="small" title="Observability (Diagnose)" style={{ marginBottom: 16 }}>
                        <Row gutter={[12, 12]}>
                            <Col xs={24} sm={8}>
                                <Card size="small">
                                    <Statistic
                                        title="Coverage-Samples"
                                        value={data.coverageSampleCount ?? 0}
                                    />
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {OPERATOR_REPLAY_COPY.observabilityCoverageFootnote}
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
                                        {OPERATOR_REPLAY_COPY.observabilityOfflineSyncedFootnote}
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
                                        {OPERATOR_REPLAY_COPY.observabilityFinalFailureFootnote}
                                    </Typography.Text>
                                </Card>
                            </Col>
                        </Row>
                    </Card>

                    <Card title="Zahlungen / Belege in dieser Batch" size="small">
                        <Alert
                            type="info"
                            showIcon
                            style={{ marginBottom: 12 }}
                            message={OPERATOR_REPLAY_COPY.paymentsDtoGapTitle}
                            description={
                                <span>
                                    <Typography.Text code>ReplayBatchPaymentItemDto</Typography.Text>{' '}
                                    {OPERATOR_REPLAY_COPY.paymentsDtoGapBody}
                                </span>
                            }
                        />
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 12 }}
                            message="FinanzOnline-Hinweis"
                            description={
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    Diese Tabelle enthält keine FO-Statusfelder. FO je Zahlung bitte über den{' '}
                                    <Link
                                        href={buildFinanzOnlineQueueInvestigationHref({
                                            investigationBatchCorrelationId: String(data.correlationId),
                                        })}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                                    </Link>{' '}
                                    prüfen.
                                </Typography.Text>
                            }
                        />
                        <Table<ReplayBatchPaymentItemDto>
                            columns={columns}
                            dataSource={data.payments ?? []}
                            rowKey={(r) => r.paymentId ?? r.offlineTransactionId ?? r.receiptId ?? 'unknown'}
                            loading={isLoading}
                            pagination={false}
                            size="small"
                            scroll={{ x: 900 }}
                            expandable={{
                                expandedRowRender: (r) => (
                                    <div style={{ padding: '4px 8px 12px', background: '#fafafa' }}>
                                        <Typography.Text strong style={{ fontSize: 12 }}>
                                            Zeile – Kontext
                                        </Typography.Text>
                                        <Descriptions bordered size="small" column={1} style={{ marginTop: 8 }}>
                                            <Descriptions.Item label="Batch-Correlation">
                                                <Typography.Text code copyable>
                                                    {String(data.correlationId ?? '—')}
                                                </Typography.Text>
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Audit-Correlation">
                                                <Typography.Text code copyable>
                                                    {data.auditCorrelationId ?? '—'}
                                                </Typography.Text>
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Offline-Transaktion">
                                                {r.offlineTransactionId?.trim() ? (
                                                    <Typography.Text code copyable>
                                                        {r.offlineTransactionId}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="PaymentId">
                                                {r.paymentId ? (
                                                    <Typography.Text code copyable>
                                                        {r.paymentId}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="FinanzOnline-Abgleich (Kontext)">
                                                {r.paymentId ? (
                                                    <Link
                                                        href={buildFinanzOnlineQueueInvestigationHref({
                                                            focusPaymentId: String(r.paymentId),
                                                            investigationBatchCorrelationId: String(data.correlationId ?? ''),
                                                        })}
                                                        target="_blank"
                                                        rel="noopener noreferrer"
                                                    >
                                                        {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                                                    </Link>
                                                ) : (
                                                    <Typography.Text type="secondary">—</Typography.Text>
                                                )}
                                            </Descriptions.Item>
                                        </Descriptions>
                                    </div>
                                ),
                            }}
                        />
                    </Card>
                </>
            )}

            {!data && !error && isLoading && (
                <Card>
                    <Typography.Text type="secondary">{OPERATOR_SHARED_COPY.loadingBatchDetail}</Typography.Text>
                </Card>
            )}

            {!data && !error && !isLoading && correlationId && (
                <Card>
                    <Typography.Text type="secondary">
                        {OPERATOR_SHARED_COPY.emptyBatchForCorrelation}
                    </Typography.Text>
                </Card>
            )}
        </>
    );
}
