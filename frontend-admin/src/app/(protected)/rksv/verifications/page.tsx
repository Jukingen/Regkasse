'use client';

import React from 'react';
import { Card, Table, Tag, Typography, Switch, Space, Alert, Tooltip } from 'antd';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useGetApiAuditLog, useGetApiAuditLogCorrelationCorrelationId } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model';
import dayjs from 'dayjs';
import { AdminTruthBadge, adminTruthTooltip } from '@/shared/adminTruthBadges';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildReplayBatchDetailHref,
    buildVerificationsAuditHref,
} from '@/shared/investigationNavigation';
import {
    OPERATOR_LINK_LABELS,
    OPERATOR_SHARED_COPY,
    OPERATOR_VERIFICATIONS_COPY,
} from '@/shared/operatorTruthCopy';
import { RKSv_ADMIN_CONTRACT_GAPS } from '@/shared/rksvAdminTruth';
import {
    auditLogMatchesVerificationsKeywordSample,
    viewAuditLogEntityDeepLinks,
    viewAuditLogStatusPresentation,
} from '@/shared/verificationsAuditView';

export default function RksvVerificationsPage() {
    const searchParams = useSearchParams();
    const correlationId = searchParams?.get('correlationId') ?? undefined;

    const { data, isLoading } = useGetApiAuditLog({ page: 1, pageSize: 100 });
    const { data: correlationData, isLoading: correlationLoading } = useGetApiAuditLogCorrelationCorrelationId(
        correlationId ?? '',
        { query: { enabled: !!correlationId } },
    );

    const useCorrelation = !!correlationId;
    const list = useCorrelation ? (correlationData?.auditLogs ?? []) : (data?.auditLogs ?? []);
    const isLoadingList = useCorrelation ? correlationLoading : isLoading;

    const signatureEntries = React.useMemo(() => {
        const base = useCorrelation ? list : (data?.auditLogs ?? []);
        return base.filter((e: AuditLogEntryDto) => auditLogMatchesVerificationsKeywordSample(e));
    }, [useCorrelation, list, data?.auditLogs]);

    const [offlineOriginOnly, setOfflineOriginOnly] = React.useState(false);
    const [failedReplayOnly, setFailedReplayOnly] = React.useState(false);
    const [suspiciousTimingOnly, setSuspiciousTimingOnly] = React.useState(false);

    const filteredEntries = React.useMemo(() => {
        return signatureEntries.filter((e: AuditLogEntryDto) => {
            const action = String(e.action ?? '').toLowerCase();
            const entity = String(e.entityType ?? '').toLowerCase();

            if (offlineOriginOnly) {
                const isOfflineRelated =
                    action.includes('offline') || entity.includes('offlinetransaction');
                if (!isOfflineRelated) return false;
            }

            if (failedReplayOnly) {
                const isFailed =
                    action.includes('offline_replay_failed') ||
                    action.includes('offline_replay_exception') ||
                    action.includes('max_retry_limit_exceeded') ||
                    action.includes('payload_immutable_mismatch') ||
                    action.includes('sequence_duplicate');
                if (!isFailed) return false;
            }

            if (suspiciousTimingOnly) {
                if (!action.includes('clock_drift_warning')) return false;
            }

            return true;
        });
    }, [signatureEntries, offlineOriginOnly, failedReplayOnly, suspiciousTimingOnly]);

    const apiRows = list.length;
    const keywordRows = signatureEntries.length;
    const displayedRows = filteredEntries.length;

    const columns = [
        {
            title: (
                <Tooltip title={OPERATOR_VERIFICATIONS_COPY.correlationColumnTooltip}>
                    <span>Correlation</span>
                </Tooltip>
            ),
            key: 'correlationId',
            width: 200,
            render: (_: unknown, r: AuditLogEntryDto) => {
                const c = r.correlationId?.trim();
                if (!c) return <Typography.Text type="secondary">—</Typography.Text>;
                return (
                    <Space direction="vertical" size={2}>
                        <Typography.Text code copyable ellipsis style={{ maxWidth: 180 }}>
                            {c}
                        </Typography.Text>
                        <Link href={buildVerificationsAuditHref(c)}>{OPERATOR_VERIFICATIONS_COPY.filterByThisCorrelationLabel}</Link>
                    </Space>
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_VERIFICATIONS_COPY.rowSourceBadgeTooltip}>
                    <span>Quelle</span>
                </Tooltip>
            ),
            key: 'source',
            width: 110,
            render: () => (
                <Tooltip title={OPERATOR_VERIFICATIONS_COPY.rowSourceBadgeTooltip}>
                    <Tag>{OPERATOR_VERIFICATIONS_COPY.rowSourceBadgeShort}</Tag>
                </Tooltip>
            ),
        },
        {
            title: 'Zeit',
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 168,
            render: (ts: string) => (ts ? dayjs(ts).format('DD.MM.YYYY HH:mm:ss') : '—'),
        },
        {
            title: 'Benutzer',
            key: 'userName',
            width: 120,
            render: (_: unknown, r: AuditLogEntryDto) => r.actorDisplayName ?? r.userId ?? '—',
        },
        {
            title: 'Aktion',
            dataIndex: 'action',
            key: 'action',
            width: 200,
            render: (a: string | null | undefined) => <Tag color="blue">{a ?? '—'}</Tag>,
        },
        {
            title: 'Entität',
            dataIndex: 'entityType',
            key: 'entityType',
            width: 110,
        },
        {
            title: 'Entity-ID',
            dataIndex: 'entityId',
            key: 'entityId',
            width: 120,
            ellipsis: true,
            render: (id: string | null | undefined) =>
                id?.trim() ? (
                    <Typography.Text code copyable ellipsis>
                        {id}
                    </Typography.Text>
                ) : (
                    '—'
                ),
        },
        {
            title: (
                <Tooltip title={OPERATOR_VERIFICATIONS_COPY.linksColumnTooltip}>
                    <span>Deep-Links</span>
                </Tooltip>
            ),
            key: 'links',
            width: 160,
            render: (_: unknown, r: AuditLogEntryDto) => {
                const { paymentListHref, receiptDetailHref } = viewAuditLogEntityDeepLinks(r);
                if (!paymentListHref && !receiptDetailHref) {
                    return <Typography.Text type="secondary">—</Typography.Text>;
                }
                return (
                    <Space direction="vertical" size={4}>
                        {paymentListHref ? (
                            <Link href={paymentListHref} target="_blank" rel="noopener noreferrer">
                                Zahlung
                            </Link>
                        ) : null}
                        {receiptDetailHref ? (
                            <Link href={receiptDetailHref} target="_blank" rel="noopener noreferrer">
                                Beleg
                            </Link>
                        ) : null}
                    </Space>
                );
            },
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            width: 130,
            render: (_: unknown, r: AuditLogEntryDto) => {
                const p = viewAuditLogStatusPresentation(r.status);
                return <Tag color={p.antColor}>{p.label}</Tag>;
            },
        },
        {
            title: 'Details',
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
        },
    ];

    return (
        <>
            <AdminPageHeader
                title={OPERATOR_VERIFICATIONS_COPY.pageTitle}
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: OPERATOR_VERIFICATIONS_COPY.breadcrumbTitle },
                ]}
            />

            <Card>
                {correlationId && (
                    <Alert
                        type="info"
                        showIcon
                        message={OPERATOR_VERIFICATIONS_COPY.filteredBannerTitle}
                        style={{ marginBottom: 16 }}
                        description={
                            <Space direction="vertical" size={10} style={{ width: '100%' }}>
                                <Space wrap align="center">
                                    <Tooltip title={adminTruthTooltip('diagnostic_support')}>
                                        <span>
                                            <AdminTruthBadge kind="diagnostic_support" />
                                        </span>
                                    </Tooltip>
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {OPERATOR_VERIFICATIONS_COPY.diagnosticLine}
                                    </Typography.Text>
                                </Space>
                                <Typography.Text code copyable>
                                    {correlationId}
                                </Typography.Text>
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    <strong>{OPERATOR_SHARED_COPY.investigateFurtherLabel}:</strong>
                                </Typography.Text>
                                <Space wrap split={<Typography.Text type="secondary">·</Typography.Text>}>
                                    {[
                                        <Link key="inc" href={buildIncidentInvestigationHref(correlationId)}>
                                            {OPERATOR_LINK_LABELS.incidentAggregate}
                                        </Link>,
                                        <Link key="rb" href={buildReplayBatchDetailHref(correlationId)}>
                                            {OPERATOR_LINK_LABELS.replayBatchDetail}
                                        </Link>,
                                        <Link
                                            key="fo"
                                            href={buildFinanzOnlineQueueInvestigationHref({
                                                investigationBatchCorrelationId: correlationId,
                                            })}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                        >
                                            {OPERATOR_LINK_LABELS.finanzQueueContext}
                                        </Link>,
                                    ]}
                                </Space>
                            </Space>
                        }
                    />
                )}

                <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                    {useCorrelation
                        ? OPERATOR_VERIFICATIONS_COPY.filteredSummary(apiRows, keywordRows, displayedRows)
                        : OPERATOR_VERIFICATIONS_COPY.unfilteredSummary(apiRows, keywordRows, displayedRows)}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
                    {OPERATOR_VERIFICATIONS_COPY.keywordSampleFootnote}
                </Typography.Paragraph>

                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message="Vertragsgrenze"
                    description={
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {RKSv_ADMIN_CONTRACT_GAPS.verificationsAuditVsSignatureDebug}{' '}
                            {RKSv_ADMIN_CONTRACT_GAPS.receiptSignatureDebugResponse}
                        </Typography.Text>
                    }
                />

                <Space direction="horizontal" wrap style={{ marginBottom: 12 }}>
                    <Space direction="horizontal">
                        <Typography.Text>Offline-Ursprung</Typography.Text>
                        <Switch checked={offlineOriginOnly} onChange={setOfflineOriginOnly} />
                    </Space>
                    <Space direction="horizontal">
                        <Typography.Text>Fehlerhafte Replays</Typography.Text>
                        <Switch checked={failedReplayOnly} onChange={setFailedReplayOnly} />
                    </Space>
                    <Space direction="horizontal">
                        <Typography.Text>Verdächtige Offline-Zeit</Typography.Text>
                        <Switch checked={suspiciousTimingOnly} onChange={setSuspiciousTimingOnly} />
                    </Space>
                </Space>
                <Table
                    columns={columns}
                    dataSource={filteredEntries}
                    loading={isLoadingList}
                    rowKey={(r) => r.id ?? `${r.timestamp ?? ''}-${r.action ?? ''}-${r.entityId ?? ''}`}
                    pagination={false}
                    size="small"
                    scroll={{ x: 1200 }}
                />
            </Card>
        </>
    );
}
