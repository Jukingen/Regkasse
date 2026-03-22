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
    Descriptions,
} from 'antd';
import { ReloadOutlined, SyncOutlined } from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import dayjs, { type Dayjs } from 'dayjs';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { parseAuthoritativeRegisterGuid, toLinkSafeRegisterRowId } from '@/shared/utils/registerIdentity';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildReplayBatchDetailHref,
} from '@/shared/investigationNavigation';
import { registerDeepLinkEligibleBadgeKind } from '@/shared/adminTruthFacets';
import { viewFinanzReconciliationRegister } from '@/shared/rksvAdminTruth';
import { AdminTruthBadge, adminTruthTooltip } from '@/shared/adminTruthBadges';
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
import { OperatorBusinessSection, OperatorSummaryStrip } from '@/shared/operatorTriageLayout';
import {
    finanzOnlineRetryUiPresentation,
    getFinanzOnlineRetryUiState,
} from '@/shared/foReconciliationRowTriage';
import {
    OPERATOR_FO_QUEUE_COPY,
    OPERATOR_INVESTIGATION_CONTEXT_COPY,
    OPERATOR_LINK_LABELS,
    OPERATOR_SHARED_COPY,
} from '@/shared/operatorTruthCopy';

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
        return parseAuthoritativeRegisterGuid(raw) ?? undefined;
    }, [searchParams]);

    /** Query contained a non-UUID cashRegisterId — never applied to API filter or Select (honest UI). */
    const rejectedRegisterQueryParam = useMemo(() => {
        const raw = searchParams?.get('cashRegisterId');
        if (raw == null || raw.trim() === '') return undefined;
        return parseAuthoritativeRegisterGuid(raw) ? undefined : raw.trim();
    }, [searchParams]);
    const initialDateRange = useMemo<[Dayjs | null, Dayjs | null]>(() => {
        const from = searchParams?.get('fromUtc');
        const to = searchParams?.get('toUtc');
        const fromDayjs = from && dayjs(from).isValid() ? dayjs(from) : null;
        const toDayjs = to && dayjs(to).isValid() ? dayjs(to) : null;
        return [fromDayjs, toDayjs];
    }, [searchParams]);

    /** Client-side row highlight only; omitted from URL if not a valid payment UUID. */
    const focusPaymentId = useMemo(() => {
        const raw = searchParams?.get('focusPaymentId');
        return parseAuthoritativeRegisterGuid(raw) ?? undefined;
    }, [searchParams]);

    const rejectedFocusPaymentParam = useMemo(() => {
        const raw = searchParams?.get('focusPaymentId');
        if (raw == null || raw.trim() === '') return undefined;
        return parseAuthoritativeRegisterGuid(raw) ? undefined : raw.trim();
    }, [searchParams]);

    /** Display-only batch correlation carried across screens; does not change reconciliation API params. */
    const investigationBatchCorrelationId = useMemo(() => {
        const raw = searchParams?.get('investigationBatchCorrelationId')?.trim();
        if (!raw) return undefined;
        return raw.slice(0, 256);
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
        const fk = parseAuthoritativeRegisterGuid(cashRegisterId);
        if (fk) p.cashRegisterId = fk;
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
            title: (
                <Tooltip title={OPERATOR_FO_QUEUE_COPY.foStatusColumnTooltip}>
                    <span>FO-Status</span>
                </Tooltip>
            ),
            dataIndex: 'finanzOnlineStatus',
            key: 'finanzOnlineStatus',
            width: 130,
            render: (val: string | null) => (
                <Tag color={statusBadgeColor(val)}>{val ?? '—'}</Tag>
            ),
        },
        {
            title: (
                <Tooltip title={OPERATOR_FO_QUEUE_COPY.foActionColumnTooltip}>
                    <span>FO-Aktion (UI)</span>
                </Tooltip>
            ),
            key: 'foRetryUi',
            width: 118,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const ui = finanzOnlineRetryUiPresentation(getFinanzOnlineRetryUiState(r.finanzOnlineStatus));
                return (
                    <Tooltip title={ui.tooltip}>
                        <Tag color={ui.tagColor}>{ui.tagLabel}</Tag>
                    </Tooltip>
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_FO_QUEUE_COPY.foTimelineColumnTooltip}>
                    <span>FO-Verlauf (API)</span>
                </Tooltip>
            ),
            key: 'foTimeline',
            width: 148,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => (
                <Space direction="vertical" size={0}>
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        Anlage:{' '}
                        {r.createdAt && dayjs(r.createdAt).isValid()
                            ? dayjs(r.createdAt).format('DD.MM. HH:mm')
                            : '—'}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        Retries: {r.finanzOnlineRetryCount ?? 0}
                    </Typography.Text>
                    <Typography.Text style={{ fontSize: 11 }}>
                        Letzter Versuch:{' '}
                        {r.finanzOnlineLastAttemptAtUtc && dayjs(r.finanzOnlineLastAttemptAtUtc).isValid()
                            ? dayjs(r.finanzOnlineLastAttemptAtUtc).format('DD.MM. HH:mm')
                            : '—'}
                    </Typography.Text>
                </Space>
            ),
        },
        {
            title: (
                <Tooltip title={OPERATOR_FO_QUEUE_COPY.foErrorShortTooltip}>
                    <span>Fehler (Kurz)</span>
                </Tooltip>
            ),
            dataIndex: 'finanzOnlineError',
            key: 'finanzOnlineError',
            width: 140,
            ellipsis: true,
            render: (v: string | null) =>
                v ? (
                    <Tooltip title={v}>
                        <Typography.Text type="danger" ellipsis style={{ maxWidth: 132 }}>
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
            title: (
                <Tooltip title={adminTruthTooltip('authoritative_api')}>
                    <span>Register (FK)</span>
                </Tooltip>
            ),
            key: 'cashRegisterId',
            width: 128,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const v = viewFinanzReconciliationRegister(r);
                if (!v.apiCashRegisterId) {
                    return (
                        <Space direction="vertical" size={2}>
                            <Typography.Text type="secondary">—</Typography.Text>
                            <AdminTruthBadge kind="link_incomplete" />
                        </Space>
                    );
                }
                return (
                    <Space direction="vertical" size={4}>
                        <Typography.Text
                            code
                            copyable={{ text: v.apiCashRegisterId }}
                            style={{ fontSize: 11 }}
                        >
                            {v.finanzQueueRegisterRowId
                                ? `${v.apiCashRegisterId.slice(0, 8)}…`
                                : v.apiCashRegisterId}
                        </Typography.Text>
                        <AdminTruthBadge
                            kind={registerDeepLinkEligibleBadgeKind({
                                linkSafeUuid: v.finanzQueueRegisterRowId,
                            })}
                        />
                        {v.registerFkRawNotLinkSafe ? (
                            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                Rohwert sichtbar; kein UUID für sichere Kassen-URLs.
                            </Typography.Text>
                        ) : null}
                    </Space>
                );
            },
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
                    <Tooltip title={OPERATOR_SHARED_COPY.refetchHintToolbar}>
                        <Button
                            icon={<ReloadOutlined />}
                            onClick={() => {
                                queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
                                queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.metrics });
                            }}
                        >
                            Aktualisieren
                        </Button>
                    </Tooltip>
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

            {rejectedRegisterQueryParam ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={OPERATOR_FO_QUEUE_COPY.queryRejectedRegisterTitle}
                    description={OPERATOR_FO_QUEUE_COPY.queryRejectedRegisterDescription(rejectedRegisterQueryParam)}
                />
            ) : null}

            {rejectedFocusPaymentParam ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={OPERATOR_FO_QUEUE_COPY.queryRejectedFocusPaymentTitle}
                    description={OPERATOR_FO_QUEUE_COPY.queryRejectedFocusPaymentDescription(
                        rejectedFocusPaymentParam,
                    )}
                />
            ) : null}

            {investigationBatchCorrelationId ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={OPERATOR_INVESTIGATION_CONTEXT_COPY.bannerTitle}
                    description={
                        <Space direction="vertical" size={8}>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {OPERATOR_INVESTIGATION_CONTEXT_COPY.bannerBody}
                            </Typography.Text>
                            {focusPaymentId ? (
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {OPERATOR_INVESTIGATION_CONTEXT_COPY.focusPaymentLine}{' '}
                                    <Typography.Text code>{focusPaymentId}</Typography.Text>
                                </Typography.Text>
                            ) : null}
                            <Typography.Text code copyable>
                                {investigationBatchCorrelationId}
                            </Typography.Text>
                            <Space wrap>
                                <Link href={buildIncidentInvestigationHref(investigationBatchCorrelationId)}>
                                    {OPERATOR_LINK_LABELS.incidentAggregate}
                                </Link>
                                <Typography.Text type="secondary">·</Typography.Text>
                                <Link href={buildReplayBatchDetailHref(investigationBatchCorrelationId)}>
                                    {OPERATOR_LINK_LABELS.replayBatchDetail}
                                </Link>
                                <Typography.Text type="secondary">·</Typography.Text>
                                <Link
                                    href={buildFinanzOnlineQueueInvestigationHref({
                                        registerRowId: toLinkSafeRegisterRowId(cashRegisterId),
                                        focusPaymentId,
                                        investigationBatchCorrelationId,
                                        fromUtc: dateRange[0]?.toISOString(),
                                        toUtc: dateRange[1]?.endOf('day').toISOString(),
                                        statusCsv: statusFilter.length ? statusFilter.join(',') : undefined,
                                    })}
                                >
                                    {OPERATOR_INVESTIGATION_CONTEXT_COPY.syncUrlWithFiltersLink}
                                </Link>
                            </Space>
                        </Space>
                    }
                />
            ) : focusPaymentId ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={OPERATOR_INVESTIGATION_CONTEXT_COPY.focusPaymentOnlyTitle}
                    description={
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {OPERATOR_INVESTIGATION_CONTEXT_COPY.focusPaymentOnlyBody}
                        </Typography.Text>
                    }
                />
            ) : null}

            {listError && (
                <Alert
                    type="error"
                    message={OPERATOR_SHARED_COPY.loadFailedList}
                    description={
                        listError instanceof Error ? listError.message : OPERATOR_SHARED_COPY.unknownErrorDetail
                    }
                    style={{ marginBottom: 16 }}
                    showIcon
                />
            )}

            <OperatorSummaryStrip>
                <Row gutter={[16, 16]}>
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
                <Typography.Paragraph type="secondary" style={{ marginTop: 14, marginBottom: 0, fontSize: 12 }}>
                    {OPERATOR_FO_QUEUE_COPY.summaryReconciliationParagraph}
                </Typography.Paragraph>
            </OperatorSummaryStrip>

            <OperatorBusinessSection
                title={OPERATOR_FO_QUEUE_COPY.businessSectionTitle}
                description={OPERATOR_FO_QUEUE_COPY.businessSectionDescription}
            >
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
                                .filter((r): r is typeof r & { id: string } => typeof r.id === 'string' && r.id.length > 0)
                                .map((r) => ({
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
                        message={OPERATOR_FO_QUEUE_COPY.emptyListTitle}
                        description={OPERATOR_FO_QUEUE_COPY.emptyListDescription}
                        showIcon
                    />
                ) : (
                    <Table<FinanzOnlineReconciliationItemDto>
                        columns={columns}
                        dataSource={items}
                        rowKey={(row) => row.paymentId ?? `${row.receiptNumber}-${row.createdAt}`}
                        loading={listLoading}
                        onRow={(record) => ({
                            style:
                                focusPaymentId && record.paymentId === focusPaymentId
                                    ? { backgroundColor: 'rgba(24, 144, 255, 0.09)' }
                                    : undefined,
                        })}
                        pagination={{
                            pageSize: 50,
                            showSizeChanger: true,
                            showTotal: (total) => `Gesamt: ${total}`,
                        }}
                        size="small"
                        scroll={{ x: 1420 }}
                        expandable={{
                            expandedRowRender: (record) => (
                                <div style={{ padding: '4px 8px 12px', background: '#fafafa' }}>
                                    <Typography.Text strong style={{ fontSize: 12 }}>
                                        Zeile – technische Details (API)
                                    </Typography.Text>
                                    <Descriptions bordered size="small" column={1} style={{ marginTop: 8 }}>
                                        <Descriptions.Item label="Fehlermeldung (vollständig)">
                                            {record.finanzOnlineError?.trim() ? (
                                                <Typography.Text type="danger" copyable>
                                                    {record.finanzOnlineError}
                                                </Typography.Text>
                                            ) : (
                                                '—'
                                            )}
                                        </Descriptions.Item>
                                        <Descriptions.Item label="Zeile angelegt (createdAt)">
                                            {record.createdAt && dayjs(record.createdAt).isValid()
                                                ? dayjs(record.createdAt).format('DD.MM.YYYY HH:mm:ss')
                                                : '—'}
                                        </Descriptions.Item>
                                        <Descriptions.Item label="Letzter Versuch (finanzOnlineLastAttemptAtUtc)">
                                            {record.finanzOnlineLastAttemptAtUtc &&
                                            dayjs(record.finanzOnlineLastAttemptAtUtc).isValid()
                                                ? dayjs(record.finanzOnlineLastAttemptAtUtc).format('DD.MM.YYYY HH:mm:ss')
                                                : '—'}
                                        </Descriptions.Item>
                                        <Descriptions.Item label="Referenz (FO)">
                                            {record.finanzOnlineReferenceId?.trim() ? (
                                                <Typography.Text code copyable>
                                                    {record.finanzOnlineReferenceId}
                                                </Typography.Text>
                                            ) : (
                                                '—'
                                            )}
                                        </Descriptions.Item>
                                        <Descriptions.Item label="Hinweis (DTO-Lücken)">
                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                Keine Correlation-ID, kein Actor/Initiator und keine serverseitige
                                                Fehlerklasse (transient/permanent) pro Zeile in{' '}
                                                <Typography.Text code>FinanzOnlineReconciliationItemDto</Typography.Text>.
                                            </Typography.Text>
                                        </Descriptions.Item>
                                    </Descriptions>
                                </div>
                            ),
                        }}
                    />
                )}
            </Card>
            </OperatorBusinessSection>
        </>
    );
}
