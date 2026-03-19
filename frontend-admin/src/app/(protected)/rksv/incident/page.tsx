'use client';

/**
 * Correlation-ID–centred unified incident investigation.
 * Composes: replay batch detail, audit log by correlation, FO reconciliation (filtered by batch paymentIds).
 * Support-first: key info at top, timeline, FO attempts, payment/receipt/register links, raw audit.
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
import { getReplayBatchDetail, type ReplayBatchPaymentItemDto } from '@/api/replay-batch';
import { getReconciliationList } from '@/api/finanzonline-reconciliation';
import { customInstance } from '@/lib/axios';
import type { AuditLog } from '@/api/generated/model';

interface AuditLogsResponse {
    success?: boolean;
    auditLogs?: AuditLog[];
    totalCount?: number;
}

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

    const { data: batch, isLoading: batchLoading, error: batchError } = useQuery({
        queryKey: ['replay-batch', normalizedId],
        queryFn: () => getReplayBatchDetail(normalizedId),
        enabled: !!normalizedId && normalizedId.length >= 32,
    });

    const { data: auditData, isLoading: auditLoading } = useQuery({
        queryKey: ['audit-correlation', batch?.auditCorrelationId ?? ''],
        queryFn: async () => {
            const id = batch!.auditCorrelationId;
            const res = await customInstance<AuditLogsResponse>({
                url: `/api/AuditLog/correlation/${id}`,
                method: 'GET',
            });
            return res;
        },
        enabled: !!batch?.auditCorrelationId,
    });

    const { data: reconList } = useQuery({
        queryKey: ['reconciliation-for-incident', batch?.payments?.map((p) => p.paymentId) ?? []],
        queryFn: () => getReconciliationList({ status: 'Pending,Failed,NeedsReconciliation,Submitted', limit: 500 }),
        enabled: !!batch?.payments?.length,
    });

    const foByPayment = useMemo(() => {
        const map = new Map<string, (typeof reconList)['items'][number]>();
        if (!reconList?.items || !batch?.payments) return map;
        const ids = new Set(batch.payments.map((p) => String(p.paymentId)));
        for (const item of reconList.items) {
            if (ids.has(item.paymentId)) map.set(item.paymentId, item);
        }
        return map;
    }, [reconList, batch?.payments]);

    const auditLogs = auditData?.auditLogs ?? [];
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

    const isLoading = batchLoading || (!!batch && auditLoading);
    const notFound = !batchLoading && correlationId && !batch && !batchError;
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

            {batchError && (
                <Alert
                    type="error"
                    message="Replay-Batch konnte nicht geladen werden"
                    description={batchError instanceof Error ? batchError.message : 'Unbekannter Fehler'}
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

            {hasBatch && batch && !batchLoading && (
                <>
                    <Card size="small" title="Replay-Zusammenfassung" style={{ marginBottom: 16 }}>
                        <Space wrap>
                            <Tag color="blue">Items: {batch.totalItems}</Tag>
                            <Tag color="green">Success: {batch.successCount}</Tag>
                            <Tag color="orange">Failed/Duplicate: {batch.failedOrDuplicateCount}</Tag>
                            <Typography.Text type="secondary">
                                Correlation-ID: <Typography.Text code copyable>{String(batch.correlationId)}</Typography.Text>
                            </Typography.Text>
                            <Typography.Text type="secondary">
                                Audit-Correlation (N): <Typography.Text code copyable>{batch.auditCorrelationId}</Typography.Text>
                            </Typography.Text>
                        </Space>
                    </Card>

                    {timelineItems.length > 0 && (
                        <Card size="small" title="Timeline (Audit)" style={{ marginBottom: 16 }}>
                            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                                Replay gestartet → Dedup/Recompute/Structural → Synced/Failed → FO Submit/Retry (falls vorhanden).
                            </Typography.Paragraph>
                            <Timeline
                                items={timelineItems.map((item) => ({
                                    color: item.status === 'Success' ? 'green' : item.status === 'Failure' ? 'red' : 'blue',
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
                                    label: 'Rohes Audit-Log (JSON)',
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
