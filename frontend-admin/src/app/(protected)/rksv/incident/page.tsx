'use client';

/**
 * Correlation-ID–centred incident investigation.
 * Data source: GET /api/admin/incidents/{correlationId} (batch + audit + FO rows for batch payments).
 */

import React, { useMemo, useState } from 'react';
import {
    Card,
    Input,
    Button,
    Space,
    Tag,
    Table,
    Typography,
    Alert,
    Spin,
    Collapse,
    Timeline,
} from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import dayjs from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getIncidentInvestigation, type IncidentAuditLogEntry } from '@/api/admin-incident';
import type { ReplayBatchPaymentItemDto } from '@/api/replay-batch';
import type { FinanzOnlineReconciliationItemDto } from '@/api/finanzonline-reconciliation';

function normalizeCorrelationId(id: string): string {
    const cleaned = id.replace(/-/g, '').trim();
    if (cleaned.length !== 32) return id;
    return `${cleaned.slice(0, 8)}-${cleaned.slice(8, 12)}-${cleaned.slice(12, 16)}-${cleaned.slice(16, 20)}-${cleaned.slice(20, 32)}`;
}

function parseReplayMeta(requestData?: string | null, responseData?: string | null): { replayPath?: string; payloadRepaired?: boolean } {
    const out: { replayPath?: string; payloadRepaired?: boolean } = {};
    try {
        if (requestData) {
            const r = JSON.parse(requestData) as Record<string, unknown>;
            if (typeof r.replayPath === 'string') out.replayPath = r.replayPath;
            if (typeof r.payloadRepaired === 'boolean') out.payloadRepaired = r.payloadRepaired;
        }
        if (responseData && !out.replayPath) {
            const r = JSON.parse(responseData) as Record<string, unknown>;
            if (typeof r.replayPath === 'string') out.replayPath = r.replayPath;
            if (typeof r.payloadRepaired === 'boolean') out.payloadRepaired = r.payloadRepaired;
        }
    } catch {
        // ignore
    }
    return out;
}

function timelineLabel(action: string, description?: string | null, meta?: { replayPath?: string; payloadRepaired?: boolean }): string {
    const parts: string[] = [];
    if (action) parts.push(action);
    if (meta?.replayPath) parts.push(`ReplayPath: ${meta.replayPath}`);
    if (meta?.payloadRepaired === true) parts.push('PayloadRepaired: ja');
    if (description && description.length < 120) parts.push(description);
    else if (description) parts.push(description.slice(0, 117) + '…');
    return parts.join(' · ') || '—';
}

export default function IncidentInvestigationPage() {
    const searchParams = useSearchParams();
    const initialId = searchParams?.get('correlationId') ?? '';
    const [inputId, setInputId] = useState(initialId);
    const [correlationId, setCorrelationId] = useState(initialId);

    const normalizedId = correlationId.trim() ? normalizeCorrelationId(correlationId.trim()) : '';

    const { data: incident, isLoading: incidentLoading, error: incidentError } = useQuery({
        queryKey: ['admin-incident', normalizedId],
        queryFn: () => getIncidentInvestigation(normalizedId),
        enabled: !!normalizedId && normalizedId.length >= 32,
    });

    const batch = incident?.replayBatch;
    const hints = incident?.hints;

    const foByPayment = useMemo(() => {
        const map = new Map<string, FinanzOnlineReconciliationItemDto>();
        const rows = incident?.finanzOnlineReconciliation;
        if (!rows?.length) return map;
        for (const item of rows) {
            map.set(String(item.paymentId), item);
        }
        return map;
    }, [incident?.finanzOnlineReconciliation]);

    const auditLogs: IncidentAuditLogEntry[] = incident?.auditLogs ?? [];
    const timelineItems = useMemo(() => {
        return auditLogs
            .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime())
            .map((log) => {
                const meta = parseReplayMeta(log.requestData, log.responseData);
                return {
                    key: log.id ?? log.timestamp,
                    timestamp: log.timestamp,
                    action: log.action,
                    status: log.status,
                    description: log.description,
                    meta,
                    full: log,
                };
            });
    }, [auditLogs]);

    const onSearch = () => {
        const id = inputId.trim();
        if (id) setCorrelationId(id);
    };

    const isLoading = incidentLoading;
    const notFound = !incidentLoading && correlationId && !batch && !incidentError;
    const hasBatch = !!batch;

    const paymentColumns = [
        {
            title: 'Zahlung',
            key: 'payment',
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => (
                <Link href={`/payments?paymentId=${r.paymentId}`} target="_blank" rel="noopener noreferrer">
                    <Typography.Text code>{String(r.paymentId).slice(0, 8)}…</Typography.Text>
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
            title: 'FO Status',
            key: 'fo',
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                if (!fo) return <Typography.Text type="secondary">—</Typography.Text>;
                const color = fo.finanzOnlineStatus === 'Submitted' ? 'green' : fo.finanzOnlineStatus === 'Failed' ? 'red' : 'orange';
                return (
                    <Space direction="vertical" size={0}>
                        <Tag color={color}>{fo.finanzOnlineStatus ?? '—'}</Tag>
                        {fo.finanzOnlineError && (
                            <Typography.Text type="secondary" style={{ fontSize: 11 }} ellipsis>
                                {fo.finanzOnlineError}
                            </Typography.Text>
                        )}
                        {fo.finanzOnlineRetryCount > 0 && (
                            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                Retries: {fo.finanzOnlineRetryCount}
                            </Typography.Text>
                        )}
                    </Space>
                );
            },
        },
        {
            title: 'Kasse',
            key: 'register',
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                const crId = fo?.cashRegisterId;
                return crId ? <Typography.Text code copyable>{crId.slice(0, 8)}…</Typography.Text> : <Typography.Text type="secondary">—</Typography.Text>;
            },
        },
        {
            title: 'Betrag',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            render: (v: number) => (v != null ? `€ ${Number(v).toFixed(2)}` : '—'),
        },
    ];

    return (
        <>
            <AdminPageHeader
                title="Incident (Correlation-ID)"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Incident' },
                ]}
            />

            <Card size="small" style={{ marginBottom: 16 }}>
                <Space.Compact style={{ width: '100%', maxWidth: 520 }}>
                    <Input
                        placeholder="Correlation-ID (mit oder ohne Bindestriche)"
                        value={inputId}
                        onChange={(e) => setInputId(e.target.value)}
                        onPressEnter={onSearch}
                        allowClear
                    />
                    <Button type="primary" icon={<SearchOutlined />} onClick={onSearch}>
                        Suchen
                    </Button>
                </Space.Compact>
            </Card>

            {incidentError && (
                <Alert
                    type="error"
                    message="Incident-Daten konnten nicht geladen werden"
                    description={incidentError instanceof Error ? incidentError.message : 'Unbekannter Fehler'}
                    style={{ marginBottom: 16 }}
                />
            )}

            {notFound && (
                <Alert
                    type="info"
                    message="Keine Daten"
                    description="Für diese Correlation-ID wurde kein Replay-Batch gefunden."
                    style={{ marginBottom: 16 }}
                />
            )}

            {isLoading && (
                <Card>
                    <Spin tip="Lade Batch, Audit-Log und FO-Daten…" />
                </Card>
            )}

            {hasBatch && batch && !incidentLoading && (
                <>
                    <Card size="small" title="Replay-Zusammenfassung" style={{ marginBottom: 16 }}>
                        <Space wrap>
                            <Tag color="blue">Items: {batch.totalItems}</Tag>
                            <Tag color="green">Fiskalisiert: {batch.successCount}</Tag>
                            <Tag color="orange">Fehler/Duplikat: {batch.failedOrDuplicateCount}</Tag>
                            {batch.offlineSyncedAuditCount != null && (
                                <Tag>Audit OFFLINE_SYNCED: {batch.offlineSyncedAuditCount}</Tag>
                            )}
                            {batch.offlineFinalFailureAuditCount != null && batch.offlineFinalFailureAuditCount > 0 && (
                                <Tag color="red">Audit Fail (final): {batch.offlineFinalFailureAuditCount}</Tag>
                            )}
                            {batch.coverageSampleCount != null && (
                                <Tag color="default">Coverage-Samples: {batch.coverageSampleCount}</Tag>
                            )}
                            <Typography.Text type="secondary">
                                Correlation-ID: <Typography.Text code copyable>{String(batch.correlationId)}</Typography.Text>
                            </Typography.Text>
                            <Typography.Text type="secondary">
                                Audit-Correlation (N): <Typography.Text code copyable>{batch.auditCorrelationId}</Typography.Text>
                            </Typography.Text>
                        </Space>
                    </Card>

                    {hints &&
                        (hints.hasLockTimeoutAudit ||
                            hints.hasPayloadImmutableMismatchAudit ||
                            hints.finanzOnlineOpenOrProblemCount > 0) && (
                            <Card size="small" title="Hinweise (Support)" style={{ marginBottom: 16 }}>
                                <Space direction="vertical" size="small">
                                    {hints.hasLockTimeoutAudit && (
                                        <Alert type="warning" showIcon message="Advisory-Lock-Timeout im Batch-Audit erkannt" />
                                    )}
                                    {hints.hasPayloadImmutableMismatchAudit && (
                                        <Alert type="error" showIcon message="Payload-Immutable-Mismatch im Batch-Audit" />
                                    )}
                                    <Typography.Text type="secondary">
                                        FinanzOnline: {hints.finanzOnlineSubmittedCount} submitted,{' '}
                                        {hints.finanzOnlineOpenOrProblemCount} offen / prüfen
                                    </Typography.Text>
                                </Space>
                            </Card>
                        )}

                    {timelineItems.length > 0 && (
                        <Card size="small" title="Timeline (Audit)" style={{ marginBottom: 16 }}>
                            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                                Replay gestartet → Dedup/Recompute/Structural → Synced/Failed → FO Submit/Retry (falls vorhanden).
                            </Typography.Paragraph>
                            <Timeline
                                items={timelineItems.map((item) => ({
                                    color:
                                        item.status === 'Success' || item.status === 0
                                            ? 'green'
                                            : item.status === 'Failure' || item.status === 1
                                              ? 'red'
                                              : 'blue',
                                    children: (
                                        <div>
                                            <Typography.Text strong>{dayjs(item.timestamp).format('DD.MM.YYYY HH:mm:ss')}</Typography.Text>
                                            <br />
                                            <Typography.Text type="secondary">
                                                {timelineLabel(item.action, item.description, item.meta)}
                                            </Typography.Text>
                                            {(item.meta?.replayPath || item.meta?.payloadRepaired !== undefined) && (
                                                <div style={{ marginTop: 4 }}>
                                                    {item.meta.replayPath && (
                                                        <Tag>ReplayPath: {item.meta.replayPath}</Tag>
                                                    )}
                                                    {item.meta.payloadRepaired === true && <Tag color="orange">PayloadRepaired</Tag>}
                                                </div>
                                            )}
                                        </div>
                                    ),
                                }))}
                            />
                        </Card>
                    )}

                    {auditLogs.length > 0 && (
                        <Card size="small" title="Audit (Struktur)" style={{ marginBottom: 16 }}>
                            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                                Sortiert nach Zeit — Replay-Pfad und Payload-Reparatur aus Request/Response extrahiert (wo vorhanden).
                            </Typography.Paragraph>
                            <Table
                                size="small"
                                scroll={{ x: 900 }}
                                pagination={{ pageSize: 12 }}
                                rowKey={(r) => String(r.id ?? r.timestamp)}
                                dataSource={[...auditLogs].sort(
                                    (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
                                )}
                                columns={[
                                    {
                                        title: 'Zeit',
                                        width: 152,
                                        render: (_: unknown, r: IncidentAuditLogEntry) =>
                                            dayjs(r.timestamp).format('DD.MM.YYYY HH:mm:ss'),
                                    },
                                    {
                                        title: 'Aktion',
                                        dataIndex: 'action',
                                        width: 200,
                                        ellipsis: true,
                                    },
                                    {
                                        title: 'Status',
                                        width: 88,
                                        render: (_: unknown, r: IncidentAuditLogEntry) => String(r.status),
                                    },
                                    {
                                        title: 'Entity',
                                        width: 120,
                                        ellipsis: true,
                                        render: (_: unknown, r: IncidentAuditLogEntry) => r.entityType ?? '—',
                                    },
                                    {
                                        title: 'Replay / Repair',
                                        width: 200,
                                        render: (_: unknown, r: IncidentAuditLogEntry) => {
                                            const m = parseReplayMeta(r.requestData, r.responseData);
                                            return (
                                                <Space size={4} wrap>
                                                    {m.replayPath ? <Tag>{m.replayPath}</Tag> : null}
                                                    {m.payloadRepaired === true ? (
                                                        <Tag color="orange">PayloadRepaired</Tag>
                                                    ) : null}
                                                    {!m.replayPath && m.payloadRepaired !== true ? (
                                                        <Typography.Text type="secondary">—</Typography.Text>
                                                    ) : null}
                                                </Space>
                                            );
                                        },
                                    },
                                    {
                                        title: 'Beschreibung',
                                        dataIndex: 'description',
                                        ellipsis: true,
                                    },
                                ]}
                            />
                        </Card>
                    )}

                    <Card size="small" title="Zahlungen / Belege / FO" style={{ marginBottom: 16 }}>
                        <Table
                            columns={paymentColumns}
                            dataSource={batch.payments}
                            rowKey="paymentId"
                            pagination={false}
                            size="small"
                        />
                    </Card>

                    {auditLogs.length > 0 && (
                        <Collapse
                            items={[
                                {
                                    key: 'raw',
                                    label: 'Rohes Audit-Log (JSON, vollständig)',
                                    children: (
                                        <pre style={{ fontSize: 11, maxHeight: 400, overflow: 'auto', background: '#f5f5f5', padding: 12 }}>
                                            {JSON.stringify(auditLogs, null, 2)}
                                        </pre>
                                    ),
                                },
                            ]}
                        />
                    )}
                </>
            )}
        </>
    );
}
