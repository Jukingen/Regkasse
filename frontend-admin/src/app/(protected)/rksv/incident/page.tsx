'use client';

/**
 * Correlation-ID–centred incident investigation.
 * Single source of truth: GET /api/admin/incidents/{correlationId}.
 * The endpoint returns one aggregate payload (replay batch + audit + FO rows).
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
    Tooltip,
    Descriptions,
} from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import dayjs from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getApiAdminIncidentsCorrelationId } from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { AuditLogEntryDto, ReplayBatchPaymentItemDto, FinanzOnlineReconciliationItemDto } from '@/api/generated/model';
import { AdminTruthBadge } from '@/shared/adminTruthBadges';
import { analyzeRegisterFkField } from '@/shared/utils/registerIdentity';
import {
    finanzOnlineRetryUiPresentation,
    getFinanzOnlineRetryUiState,
} from '@/shared/foReconciliationRowTriage';
import {
    OperatorBusinessSection,
    OperatorSummaryStrip,
    OperatorTechnicalSection,
} from '@/shared/operatorTriageLayout';
import { buildFinanzOnlineQueueInvestigationHref } from '@/shared/investigationNavigation';
import {
    OPERATOR_INCIDENT_COPY,
    OPERATOR_INVESTIGATION_CONTEXT_COPY,
    OPERATOR_LINK_LABELS,
    OPERATOR_SHARED_COPY,
} from '@/shared/operatorTruthCopy';

const RKSV_HANDOFF_PREFIX = 'RKSV_HANDOFF_V1:';

function normalizeCorrelationId(id: string): string {
    const cleaned = id.replace(/-/g, '').trim();
    if (cleaned.length !== 32) return id;
    return `${cleaned.slice(0, 8)}-${cleaned.slice(8, 12)}-${cleaned.slice(12, 16)}-${cleaned.slice(16, 20)}-${cleaned.slice(20, 32)}`;
}

function firstGuidLike(input: string): string | null {
    const match = input.match(/[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}/);
    return match?.[0] ?? null;
}

function resolveCorrelationInput(input: string): string {
    const raw = input.trim();
    if (!raw) return '';

    if (raw.startsWith(RKSV_HANDOFF_PREFIX)) {
        const jsonText = raw.slice(RKSV_HANDOFF_PREFIX.length).trim();
        try {
            const parsed = JSON.parse(jsonText) as { correlationId?: unknown };
            if (typeof parsed.correlationId === 'string' && parsed.correlationId.trim().length > 0) {
                return normalizeCorrelationId(parsed.correlationId.trim());
            }
        } catch {
            // ignore parse failure, continue with generic extraction
        }
    }

    try {
        if (raw.startsWith('http://') || raw.startsWith('https://')) {
            const url = new URL(raw);
            const qp = url.searchParams.get('correlationId');
            if (qp?.trim()) return normalizeCorrelationId(qp.trim());
        }
    } catch {
        // ignore URL parse failure
    }

    if (raw.startsWith('{') && raw.endsWith('}')) {
        try {
            const parsed = JSON.parse(raw) as { correlationId?: unknown; replayBatchCorrelationId?: unknown };
            const fromCorrelation = typeof parsed.correlationId === 'string' ? parsed.correlationId : null;
            const fromReplayBatch = typeof parsed.replayBatchCorrelationId === 'string' ? parsed.replayBatchCorrelationId : null;
            if (fromCorrelation?.trim()) return normalizeCorrelationId(fromCorrelation.trim());
            if (fromReplayBatch?.trim()) return normalizeCorrelationId(fromReplayBatch.trim());
        } catch {
            // ignore parse failure
        }
    }

    const guid = firstGuidLike(raw);
    return guid ? normalizeCorrelationId(guid) : normalizeCorrelationId(raw);
}

function isPlainJsonObject(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Reads optional replayPath / payloadRepaired from audit JSON blobs without asserting a full DTO. */
function parseReplayMeta(requestData?: string | null, responseData?: string | null): {
    replayPath?: string;
    payloadRepaired?: boolean;
} {
    const out: { replayPath?: string; payloadRepaired?: boolean } = {};

    const mergeFromJsonText = (json: string | null | undefined) => {
        if (!json?.trim()) {
            return;
        }
        let parsed: unknown;
        try {
            parsed = JSON.parse(json);
        } catch {
            return;
        }
        if (!isPlainJsonObject(parsed)) {
            if (process.env.NODE_ENV === 'development') {
                console.warn('[incident] parseReplayMeta: expected JSON object for replay meta');
            }
            return;
        }
        if (typeof parsed.replayPath === 'string') {
            out.replayPath = parsed.replayPath;
        }
        if (typeof parsed.payloadRepaired === 'boolean') {
            out.payloadRepaired = parsed.payloadRepaired;
        }
    };

    mergeFromJsonText(requestData);
    if (!out.replayPath) {
        mergeFromJsonText(responseData);
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
    const initialId = searchParams?.get('correlationId') ?? searchParams?.get('handoff') ?? '';
    const [inputId, setInputId] = useState(initialId);
    const [correlationId, setCorrelationId] = useState(resolveCorrelationInput(initialId));

    const normalizedId = correlationId.trim() ? normalizeCorrelationId(correlationId.trim()) : '';

    const { data: incident, isLoading: incidentLoading, error: incidentError } = useQuery({
        queryKey: rksvAdminQueryKeys.incident(normalizedId),
        queryFn: () => getApiAdminIncidentsCorrelationId(normalizedId),
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

    const auditLogs: AuditLogEntryDto[] = incident?.auditLogs ?? [];
    const timelineItems = useMemo(() => {
        return auditLogs
            .sort((a, b) => new Date(a.timestamp ?? 0).getTime() - new Date(b.timestamp ?? 0).getTime())
            .map((log) => {
                const meta = parseReplayMeta(log.requestData, log.responseData);
                return {
                    key: log.id ?? log.timestamp ?? 'unknown',
                    timestamp: log.timestamp ?? '',
                    action: log.action ?? '',
                    status: log.status,
                    description: log.description,
                    meta,
                    full: log,
                };
            });
    }, [auditLogs]);

    const onSearch = () => {
        const id = resolveCorrelationInput(inputId);
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
            title: (
                <Tooltip title={OPERATOR_INCIDENT_COPY.foStatusFromJoinTooltip}>
                    <span>FO-Status</span>
                </Tooltip>
            ),
            key: 'fo',
            width: 120,
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                if (!fo) return <Typography.Text type="secondary">—</Typography.Text>;
                const color =
                    fo.finanzOnlineStatus === 'Submitted'
                        ? 'green'
                        : fo.finanzOnlineStatus === 'Failed'
                          ? 'red'
                          : 'orange';
                return (
                    <Space direction="vertical" size={2}>
                        <Tag color={color}>{fo.finanzOnlineStatus ?? '—'}</Tag>
                        {fo.finanzOnlineError ? (
                            <Tooltip title={fo.finanzOnlineError}>
                                <Typography.Text type="secondary" style={{ fontSize: 11 }} ellipsis>
                                    {fo.finanzOnlineError}
                                </Typography.Text>
                            </Tooltip>
                        ) : null}
                    </Space>
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_INCIDENT_COPY.foActionIncidentTooltip}>
                    <span>FO-Aktion (UI)</span>
                </Tooltip>
            ),
            key: 'foRetryUi',
            width: 112,
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                const ui = finanzOnlineRetryUiPresentation(getFinanzOnlineRetryUiState(fo?.finanzOnlineStatus));
                return (
                    <Tooltip title={ui.tooltip}>
                        <Tag color={ui.tagColor}>{ui.tagLabel}</Tag>
                    </Tooltip>
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_INCIDENT_COPY.timesColumnIncidentTooltip}>
                    <span>Zeiten & Versuche</span>
                </Tooltip>
            ),
            key: 'rowTiming',
            width: 152,
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                return (
                    <Space direction="vertical" size={0}>
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            Replay:{' '}
                            {r.createdAtUtc && dayjs(r.createdAtUtc).isValid()
                                ? dayjs(r.createdAtUtc).format('DD.MM. HH:mm')
                                : '—'}
                        </Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            FO Anlage:{' '}
                            {fo?.createdAt && dayjs(fo.createdAt).isValid()
                                ? dayjs(fo.createdAt).format('DD.MM. HH:mm')
                                : '—'}
                        </Typography.Text>
                        <Typography.Text style={{ fontSize: 11 }}>
                            FO Versuch:{' '}
                            {fo?.finanzOnlineLastAttemptAtUtc && dayjs(fo.finanzOnlineLastAttemptAtUtc).isValid()
                                ? dayjs(fo.finanzOnlineLastAttemptAtUtc).format('DD.MM. HH:mm')
                                : '—'}
                        </Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            Retries: {fo?.finanzOnlineRetryCount ?? 0}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_INCIDENT_COPY.foRefColumnTooltip}>
                    <span>FO-Ref</span>
                </Tooltip>
            ),
            key: 'foRef',
            width: 100,
            ellipsis: true,
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                const ref = fo?.finanzOnlineReferenceId?.trim();
                return ref ? (
                    <Typography.Text code copyable ellipsis style={{ maxWidth: 92 }}>
                        {ref}
                    </Typography.Text>
                ) : (
                    '—'
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_INCIDENT_COPY.registerFkColumnTooltip}>
                    <span>Kasse (FK)</span>
                </Tooltip>
            ),
            key: 'register',
            render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
                const fo = foByPayment.get(String(r.paymentId));
                const reg = analyzeRegisterFkField(fo?.cashRegisterId);
                if (!fo) {
                    return <Typography.Text type="secondary">—</Typography.Text>;
                }
                if (!reg.rawTrimmed) {
                    return (
                        <Space direction="vertical" size={2}>
                            <Typography.Text type="secondary">—</Typography.Text>
                            <AdminTruthBadge kind="link_incomplete" />
                        </Space>
                    );
                }
                return (
                    <Space direction="vertical" size={4}>
                        <Typography.Text code copyable={{ text: reg.rawTrimmed }}>
                            {reg.linkSafeUuid ? `${reg.rawTrimmed.slice(0, 8)}…` : reg.rawTrimmed}
                        </Typography.Text>
                        <AdminTruthBadge kind="derived_from_foreign_row" />
                        {reg.isRawPresentButNotLinkSafe ? (
                            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                FO-Feld ohne UUID-Form — nicht für Kassen-Deep-Links verwenden.
                            </Typography.Text>
                        ) : null}
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
                    message={OPERATOR_SHARED_COPY.loadFailedIncident}
                    description={
                        incidentError instanceof Error
                            ? incidentError.message
                            : OPERATOR_SHARED_COPY.unknownErrorDetail
                    }
                    style={{ marginBottom: 16 }}
                />
            )}

            {notFound && (
                <Alert
                    type="info"
                    message={OPERATOR_SHARED_COPY.notFoundIncidentTitle}
                    description={OPERATOR_SHARED_COPY.notFoundIncidentDescription}
                    style={{ marginBottom: 16 }}
                />
            )}

            {isLoading && (
                <Card>
                    <Spin tip={OPERATOR_SHARED_COPY.loadingIncident} />
                </Card>
            )}

            {hasBatch && batch && !incidentLoading && (
                <>
                    <OperatorSummaryStrip>
                        <Space direction="vertical" size={10} style={{ width: '100%' }}>
                            <div>
                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                    Gesuchte Correlation-ID
                                </Typography.Text>
                                <Typography.Text code copyable>
                                    {normalizedId || correlationId || '—'}
                                </Typography.Text>
                            </div>
                            <Space wrap>
                                <Tag color="blue">Batch-Items: {batch.totalItems}</Tag>
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
                            </Space>
                            <Space direction="vertical" size={4}>
                                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                    Batch-Correlation
                                </Typography.Text>
                                <Typography.Text code copyable>{String(batch.correlationId)}</Typography.Text>
                                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                    Audit-Correlation
                                </Typography.Text>
                                <Typography.Text code copyable>{batch.auditCorrelationId ?? '—'}</Typography.Text>
                            </Space>
                            {hints ? (
                                <Typography.Paragraph style={{ marginBottom: 0 }} type="secondary">
                                    {OPERATOR_INCIDENT_COPY.foAggregateLine(
                                        hints.finanzOnlineSubmittedCount ?? 0,
                                        hints.finanzOnlineOpenOrProblemCount ?? 0
                                    )}
                                </Typography.Paragraph>
                            ) : null}
                            {batch.correlationId ? (
                                <Space wrap size={8}>
                                    <Typography.Text type="secondary">
                                        {OPERATOR_SHARED_COPY.investigateFurtherLabel}:
                                    </Typography.Text>
                                    <Link href={`/rksv/replay-batch/${encodeURIComponent(String(batch.correlationId))}`}>
                                        {OPERATOR_LINK_LABELS.replayBatchDetail}
                                    </Link>
                                    <Typography.Text type="secondary">·</Typography.Text>
                                    <Link
                                        href={`/rksv/verifications?correlationId=${encodeURIComponent(String(batch.auditCorrelationId ?? batch.correlationId))}`}
                                    >
                                        {OPERATOR_LINK_LABELS.verificationsAudit}
                                    </Link>
                                    <Typography.Text type="secondary">·</Typography.Text>
                                    <Link
                                        href={buildFinanzOnlineQueueInvestigationHref({
                                            investigationBatchCorrelationId: String(batch.correlationId),
                                        })}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        {OPERATOR_LINK_LABELS.finanzQueueContext}
                                    </Link>
                                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                        {OPERATOR_INVESTIGATION_CONTEXT_COPY.foRowsNoCorrelationNote}
                                    </Typography.Text>
                                </Space>
                            ) : null}
                            {hints &&
                                (hints.hasLockTimeoutAudit ||
                                    hints.hasPayloadImmutableMismatchAudit ||
                                    (hints.finanzOnlineOpenOrProblemCount ?? 0) > 0) && (
                                    <Space direction="vertical" size={8} style={{ width: '100%' }}>
                                        {hints.hasLockTimeoutAudit && (
                                            <Alert
                                                type="warning"
                                                showIcon
                                                message="Advisory-Lock-Timeout im Batch-Audit erkannt"
                                            />
                                        )}
                                        {hints.hasPayloadImmutableMismatchAudit && (
                                            <Alert type="error" showIcon message="Payload-Immutable-Mismatch im Batch-Audit" />
                                        )}
                                    </Space>
                                )}
                        </Space>
                    </OperatorSummaryStrip>

                    <OperatorBusinessSection>
                        <Card size="small" title="Zahlungen, Belege & FinanzOnline (Geschäftsobjekte)">
                            <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
                                {OPERATOR_INCIDENT_COPY.paymentsCardIntro}
                            </Typography.Paragraph>
                            <Table<ReplayBatchPaymentItemDto>
                                columns={paymentColumns}
                                dataSource={batch.payments ?? []}
                                rowKey={(row) => row.paymentId ?? row.offlineTransactionId ?? row.receiptId ?? 'unknown'}
                                pagination={false}
                                size="small"
                                scroll={{ x: 1180 }}
                                expandable={{
                                    expandedRowRender: (r) => {
                                        const fo = foByPayment.get(String(r.paymentId));
                                        return (
                                            <div style={{ padding: '4px 8px 12px', background: '#fafafa' }}>
                                                <Typography.Text strong style={{ fontSize: 12 }}>
                                                    Zeile – Detail & Nachweise
                                                </Typography.Text>
                                                <Descriptions bordered size="small" column={1} style={{ marginTop: 8 }}>
                                                    <Descriptions.Item label="Offline-Transaktion">
                                                        {r.offlineTransactionId?.trim() ? (
                                                            <Typography.Text code copyable>
                                                                {r.offlineTransactionId}
                                                            </Typography.Text>
                                                        ) : (
                                                            '—'
                                                        )}
                                                    </Descriptions.Item>
                                                    <Descriptions.Item label="FO Fehler (vollständig)">
                                                        {fo?.finanzOnlineError?.trim() ? (
                                                            <Typography.Text type="danger" copyable>
                                                                {fo.finanzOnlineError}
                                                            </Typography.Text>
                                                        ) : (
                                                            '—'
                                                        )}
                                                    </Descriptions.Item>
                                                    <Descriptions.Item label="FO Referenz (vollständig)">
                                                        {fo?.finanzOnlineReferenceId?.trim() ? (
                                                            <Typography.Text code copyable>
                                                                {fo.finanzOnlineReferenceId}
                                                            </Typography.Text>
                                                        ) : (
                                                            '—'
                                                        )}
                                                    </Descriptions.Item>
                                                    <Descriptions.Item label="Batch-Correlation (Kontext)">
                                                        <Typography.Text code copyable>
                                                            {String(batch.correlationId ?? '—')}
                                                        </Typography.Text>
                                                    </Descriptions.Item>
                                                    <Descriptions.Item label="Hinweis">
                                                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                            {OPERATOR_INCIDENT_COPY.expandDtoNote}
                                                        </Typography.Text>
                                                    </Descriptions.Item>
                                                </Descriptions>
                                            </div>
                                        );
                                    },
                                }}
                            />
                        </Card>
                    </OperatorBusinessSection>

                    <OperatorTechnicalSection>
                        {timelineItems.length > 0 && (
                            <Card size="small" title="Timeline (Audit)" style={{ marginBottom: 16 }}>
                                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                                    Replay gestartet → Dedup/Recompute/Structural → Synced/Failed → FO Submit/Retry (falls
                                    vorhanden).
                                </Typography.Paragraph>
                                <Timeline
                                    items={timelineItems.map((item) => ({
                                        color:
                                            item.status === 0
                                                ? 'green'
                                                : item.status === 1
                                                  ? 'red'
                                                  : 'blue',
                                        children: (
                                            <div>
                                                <Typography.Text strong>
                                                    {dayjs(item.timestamp).format('DD.MM.YYYY HH:mm:ss')}
                                                </Typography.Text>
                                                <br />
                                                <Typography.Text type="secondary">
                                                    {timelineLabel(item.action, item.description, item.meta)}
                                                </Typography.Text>
                                                {(item.meta?.replayPath || item.meta?.payloadRepaired !== undefined) && (
                                                    <div style={{ marginTop: 4 }}>
                                                        {item.meta.replayPath && (
                                                            <Tag>ReplayPath: {item.meta.replayPath}</Tag>
                                                        )}
                                                        {item.meta.payloadRepaired === true && (
                                                            <Tag color="orange">PayloadRepaired</Tag>
                                                        )}
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
                                    Sortiert nach Zeit — Replay-Pfad und Payload-Reparatur aus Request/Response extrahiert
                                    (wo vorhanden).
                                </Typography.Paragraph>
                                <Table
                                    size="small"
                                    scroll={{ x: 900 }}
                                    pagination={{ pageSize: 12 }}
                                    rowKey={(r) => String(r.id ?? r.timestamp)}
                                    dataSource={[...auditLogs].sort(
                                        (a, b) =>
                                            new Date(a.timestamp ?? 0).getTime() -
                                            new Date(b.timestamp ?? 0).getTime(),
                                    )}
                                    columns={[
                                        {
                                            title: 'Zeit',
                                            width: 152,
                                            render: (_: unknown, r: AuditLogEntryDto) =>
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
                                            render: (_: unknown, r: AuditLogEntryDto) => String(r.status),
                                        },
                                        {
                                            title: 'Entity',
                                            width: 120,
                                            ellipsis: true,
                                            render: (_: unknown, r: AuditLogEntryDto) => r.entityType ?? '—',
                                        },
                                        {
                                            title: 'Replay / Repair',
                                            width: 200,
                                            render: (_: unknown, r: AuditLogEntryDto) => {
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

                        {auditLogs.length > 0 && (
                            <Collapse
                                items={[
                                    {
                                        key: 'raw',
                                        label: 'Rohes Audit-Log (JSON, vollständig)',
                                        children: (
                                            <pre
                                                style={{
                                                    fontSize: 11,
                                                    maxHeight: 400,
                                                    overflow: 'auto',
                                                    background: '#f5f5f5',
                                                    padding: 12,
                                                }}
                                            >
                                                {JSON.stringify(auditLogs, null, 2)}
                                            </pre>
                                        ),
                                    },
                                ]}
                            />
                        )}
                    </OperatorTechnicalSection>
                </>
            )}
        </>
    );
}
