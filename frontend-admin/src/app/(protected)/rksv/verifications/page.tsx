'use client';

import React from 'react';
import { Card, Table, Tag, Typography, Switch, Space, Alert, Tooltip, Collapse, Descriptions, Divider } from 'antd';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
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
    OPERATOR_FO_SUMMARY_SCREEN_COPY,
    OPERATOR_LINK_LABELS,
    OPERATOR_SHARED_COPY,
    OPERATOR_VERIFICATIONS_COPY,
} from '@/shared/operatorTruthCopy';
import { RKSv_ADMIN_CONTRACT_GAPS } from '@/shared/rksvAdminTruth';
import {
    auditLogMatchesVerificationsKeywordSample,
    describeVerificationsKeywordMatchDe,
    viewAuditLogEntityDeepLinks,
    viewAuditLogStatusPresentation,
} from '@/shared/verificationsAuditView';

export default function RksvVerificationsPage() {
    const searchParams = useSearchParams();
    const correlationId = searchParams?.get('correlationId') ?? undefined;
    const useCorrelation = !!correlationId;

    const [page, setPage] = React.useState(1);
    const [pageSize, setPageSize] = React.useState(50);
    const [corrPage, setCorrPage] = React.useState(1);
    const [corrPageSize, setCorrPageSize] = React.useState(50);

    const [offlineOriginOnly, setOfflineOriginOnly] = React.useState(false);
    const [failedReplayOnly, setFailedReplayOnly] = React.useState(false);
    const [suspiciousTimingOnly, setSuspiciousTimingOnly] = React.useState(false);

    React.useEffect(() => {
        setPage(1);
    }, [correlationId]);

    React.useEffect(() => {
        setCorrPage(1);
    }, [correlationId, offlineOriginOnly, failedReplayOnly, suspiciousTimingOnly]);

    React.useEffect(() => {
        if (useCorrelation) return;
        setPage(1);
    }, [useCorrelation, offlineOriginOnly, failedReplayOnly, suspiciousTimingOnly]);

    const { data, isLoading } = useGetApiAuditLog(
        { page, pageSize },
        { query: { enabled: !useCorrelation } },
    );
    const { data: correlationData, isLoading: correlationLoading } = useGetApiAuditLogCorrelationCorrelationId(
        correlationId ?? '',
        { query: { enabled: !!correlationId } },
    );

    const list = React.useMemo(
        () => (useCorrelation ? (correlationData?.auditLogs ?? []) : (data?.auditLogs ?? [])),
        [useCorrelation, correlationData?.auditLogs, data?.auditLogs],
    );
    const isLoadingList = useCorrelation ? correlationLoading : isLoading;

    const signatureEntries = React.useMemo(() => {
        return list.filter((e: AuditLogEntryDto) => auditLogMatchesVerificationsKeywordSample(e));
    }, [list]);

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

    /**
     * Server-driven total for Ant Design async pagination.
     * When totalCount is omitted but the current page is full, allow "next" without implying a known global total.
     */
    const serverPaginationListTotal = React.useMemo(() => {
        if (useCorrelation) return filteredEntries.length;
        const floor = (page - 1) * pageSize + apiRows;
        const totalCount = data?.totalCount;
        if (totalCount != null) {
            return totalCount;
        }
        const maybeMore = apiRows === pageSize && apiRows > 0;
        return maybeMore ? floor + 1 : Math.max(floor, apiRows);
    }, [useCorrelation, filteredEntries.length, data?.totalCount, page, pageSize, apiRows]);

    const totalCountKnownForFooter =
        !useCorrelation && data != null && data.totalCount !== undefined && data.totalCount !== null;

    const showPaginationQuickJumper =
        !useCorrelation && data?.totalPages != null && data.totalPages > 4;

    const tablePagination = useCorrelation
        ? {
              current: corrPage,
              pageSize: corrPageSize,
              total: filteredEntries.length,
              showSizeChanger: true as const,
              pageSizeOptions: ['25', '50', '100'],
              hideOnSinglePage: false,
              showTotal: (_total: number, range: [number, number]) =>
                  OPERATOR_VERIFICATIONS_COPY.verificationsCorrelationPaginationShowTotal(range, filteredEntries.length),
              onChange: (p: number, ps: number) => {
                  setCorrPage(p);
                  setCorrPageSize(ps);
              },
          }
        : {
              current: page,
              pageSize,
              total: serverPaginationListTotal,
              showSizeChanger: true as const,
              pageSizeOptions: ['25', '50', '100'],
              hideOnSinglePage: false,
              showQuickJumper: showPaginationQuickJumper,
              showTotal: (_total: number, range: [number, number]) =>
                  OPERATOR_VERIFICATIONS_COPY.verificationsServerPaginationShowTotal(
                      range,
                      displayedRows,
                      apiRows,
                      totalCountKnownForFooter ? data!.totalCount! : undefined,
                  ),
              onChange: (p: number, ps: number) => {
                  setPage(p);
                  setPageSize(ps);
              },
          };

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
            title: (
                <Tooltip title={OPERATOR_VERIFICATIONS_COPY.treffergrundColumnTooltip}>
                    <span>{OPERATOR_VERIFICATIONS_COPY.treffergrundColumnTitle}</span>
                </Tooltip>
            ),
            key: 'treffergrund',
            width: 220,
            ellipsis: true,
            render: (_: unknown, r: AuditLogEntryDto) => {
                const text = describeVerificationsKeywordMatchDe(r);
                return (
                    <Tooltip title={text}>
                        <Typography.Text style={{ fontSize: 12 }}>
                            <Tag color="orange" style={{ marginRight: 6 }}>
                                {OPERATOR_VERIFICATIONS_COPY.treffergrundTagShort}
                            </Tag>
                            {text}
                        </Typography.Text>
                    </Tooltip>
                );
            },
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
                                {OPERATOR_VERIFICATIONS_COPY.deepLinkPaymentLabel}
                            </Link>
                        ) : null}
                        {receiptDetailHref ? (
                            <Link href={receiptDetailHref} target="_blank" rel="noopener noreferrer">
                                {OPERATOR_VERIFICATIONS_COPY.deepLinkReceiptLabel}
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

    const clipText = (text: string | null | undefined, max: number) => {
        const t = text?.trim();
        if (!t) return null;
        return t.length > max ? `${t.slice(0, max)}…` : t;
    };

    const renderExpandedAuditRow = (r: AuditLogEntryDto) => {
        const ctx = r.correlationId?.trim();
        const abgleichHref = ctx
            ? buildFinanzOnlineQueueInvestigationHref({ investigationBatchCorrelationId: ctx })
            : '/rksv/finanz-online-queue';
        const { paymentListHref, receiptDetailHref } = viewAuditLogEntityDeepLinks(r);
        const req = clipText(r.requestData, 4000);
        const res = clipText(r.responseData, 4000);

        return (
            <div style={{ padding: '8px 12px 16px', background: '#fafafa' }}>
                <Typography.Text strong style={{ fontSize: 12 }}>
                    {OPERATOR_VERIFICATIONS_COPY.expandPanelIntro}
                </Typography.Text>

                <Divider orientation="left" plain style={{ margin: '12px 0 8px' }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {OPERATOR_VERIFICATIONS_COPY.expandWhyTitle}
                    </Typography.Text>
                </Divider>
                <Typography.Paragraph style={{ marginBottom: 8, fontSize: 12 }}>
                    <strong>Treffergrund:</strong> {describeVerificationsKeywordMatchDe(r)}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                    {OPERATOR_VERIFICATIONS_COPY.expandWhyBody}
                </Typography.Paragraph>

                <Divider orientation="left" plain style={{ margin: '16px 0 8px' }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {OPERATOR_VERIFICATIONS_COPY.expandTechnicalTitle}
                    </Typography.Text>
                </Divider>
                <Descriptions bordered size="small" column={1}>
                    <Descriptions.Item label="action">{r.action ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="entityType">{r.entityType ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="entityId">
                        {r.entityId?.trim() ? (
                            <Typography.Text code copyable>
                                {r.entityId}
                            </Typography.Text>
                        ) : (
                            '—'
                        )}
                    </Descriptions.Item>
                    <Descriptions.Item label="correlationId">
                        {ctx ? (
                            <Typography.Text code copyable>
                                {ctx}
                            </Typography.Text>
                        ) : (
                            '—'
                        )}
                    </Descriptions.Item>
                    <Descriptions.Item label="endpoint">{r.endpoint ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="httpMethod">{r.httpMethod ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="httpStatusCode">
                        {r.httpStatusCode != null ? String(r.httpStatusCode) : '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label="errorDetails">
                        {r.errorDetails?.trim() ? (
                            <Typography.Text type="danger" copyable>
                                {r.errorDetails}
                            </Typography.Text>
                        ) : (
                            '—'
                        )}
                    </Descriptions.Item>
                    <Descriptions.Item label="description">
                        {r.description?.trim() ? r.description : '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label="timestamp">
                        {r.timestamp ? dayjs(r.timestamp).format('DD.MM.YYYY HH:mm:ss') : '—'}
                    </Descriptions.Item>
                </Descriptions>

                {(req || res) && (
                    <Collapse
                        size="small"
                        style={{ marginTop: 12 }}
                        items={[
                            ...(req
                                ? [
                                      {
                                          key: 'req',
                                          label: 'requestData (gekürzt)',
                                          children: (
                                              <Typography.Paragraph
                                                  copyable={
                                                      r.requestData
                                                          ? { text: r.requestData }
                                                          : false
                                                  }
                                                  style={{
                                                      marginBottom: 0,
                                                      fontSize: 11,
                                                      fontFamily: 'monospace',
                                                      whiteSpace: 'pre-wrap',
                                                      wordBreak: 'break-word',
                                                      maxHeight: 220,
                                                      overflow: 'auto',
                                                  }}
                                              >
                                                  {req}
                                              </Typography.Paragraph>
                                          ),
                                      },
                                  ]
                                : []),
                            ...(res
                                ? [
                                      {
                                          key: 'res',
                                          label: 'responseData (gekürzt)',
                                          children: (
                                              <Typography.Paragraph
                                                  copyable={
                                                      r.responseData
                                                          ? { text: r.responseData }
                                                          : false
                                                  }
                                                  style={{
                                                      marginBottom: 0,
                                                      fontSize: 11,
                                                      fontFamily: 'monospace',
                                                      whiteSpace: 'pre-wrap',
                                                      wordBreak: 'break-word',
                                                      maxHeight: 220,
                                                      overflow: 'auto',
                                                  }}
                                              >
                                                  {res}
                                              </Typography.Paragraph>
                                          ),
                                      },
                                  ]
                                : []),
                        ]}
                    />
                )}

                <Divider orientation="left" plain style={{ margin: '16px 0 8px' }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {OPERATOR_VERIFICATIONS_COPY.expandAuthoritativeLinksTitle}
                    </Typography.Text>
                </Divider>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                    {OPERATOR_VERIFICATIONS_COPY.expandFinanzOnlineAbgleichLead}
                </Typography.Paragraph>
                <Space direction="vertical" size={6}>
                    <Link href={abgleichHref} target="_blank" rel="noopener noreferrer">
                        {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                    </Link>
                    {ctx ? (
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 11 }}>
                            {OPERATOR_VERIFICATIONS_COPY.expandFinanzOnlineWithCorrelationHint}
                        </Typography.Paragraph>
                    ) : null}
                    <Space wrap size={[8, 8]}>
                        {paymentListHref ? (
                            <Link href={paymentListHref} target="_blank" rel="noopener noreferrer">
                                {OPERATOR_VERIFICATIONS_COPY.deepLinkPaymentLabel}
                            </Link>
                        ) : null}
                        {receiptDetailHref ? (
                            <Link href={receiptDetailHref} target="_blank" rel="noopener noreferrer">
                                {OPERATOR_VERIFICATIONS_COPY.deepLinkReceiptLabel}
                            </Link>
                        ) : null}
                        {ctx ? (
                            <Link href={buildVerificationsAuditHref(ctx)}>{OPERATOR_VERIFICATIONS_COPY.filterByThisCorrelationLabel}</Link>
                        ) : null}
                    </Space>
                </Space>
            </div>
        );
    };

    return (
        <>
            <AdminPageHeader
                title={OPERATOR_VERIFICATIONS_COPY.pageTitle}
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: 'RKSV', href: '/rksv' },
                    { title: OPERATOR_VERIFICATIONS_COPY.breadcrumbTitle },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {OPERATOR_VERIFICATIONS_COPY.pageSubtitle}
                </Typography.Paragraph>
            </AdminPageHeader>

            <Card>
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={OPERATOR_VERIFICATIONS_COPY.pageScopeBannerMessage}
                    description={OPERATOR_VERIFICATIONS_COPY.pageScopeBannerDescription}
                />

                {correlationId && (
                    <Alert
                        type="info"
                        showIcon
                        message={OPERATOR_VERIFICATIONS_COPY.filteredBannerTitle}
                        style={{ marginBottom: 12 }}
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

                <Typography.Paragraph style={{ marginBottom: 10 }}>
                    <Typography.Text strong>
                        {useCorrelation
                            ? OPERATOR_VERIFICATIONS_COPY.verificationsPrimaryStripCorrelation(
                                  displayedRows,
                                  keywordRows,
                                  apiRows,
                              )
                            : OPERATOR_VERIFICATIONS_COPY.verificationsPrimaryStripList(
                                  displayedRows,
                                  keywordRows,
                                  apiRows,
                                  { totalCount: data?.totalCount ?? undefined },
                              )}
                    </Typography.Text>
                </Typography.Paragraph>

                <Collapse
                    bordered={false}
                    ghost
                    size="small"
                    style={{ marginBottom: 12 }}
                    items={[
                        {
                            key: 'context',
                            label: OPERATOR_VERIFICATIONS_COPY.verificationsContextCollapseTitle,
                            children: (
                                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                                    {!useCorrelation ? (
                                        <>
                                            <div>
                                                <Typography.Text strong style={{ display: 'block', marginBottom: 4, fontSize: 12 }}>
                                                    {OPERATOR_VERIFICATIONS_COPY.correlationFilterHintTitle}
                                                </Typography.Text>
                                                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                                    {OPERATOR_VERIFICATIONS_COPY.correlationFilterHintBody}
                                                </Typography.Paragraph>
                                            </div>
                                            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                                {OPERATOR_VERIFICATIONS_COPY.verificationsCollapseApiPageLine(
                                                    data?.page ?? page,
                                                    data?.pageSize ?? pageSize,
                                                    data?.totalPages,
                                                )}
                                            </Typography.Paragraph>
                                            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                                {OPERATOR_VERIFICATIONS_COPY.verificationsServerPaginationNote}
                                            </Typography.Paragraph>
                                            {!isLoadingList &&
                                            data != null &&
                                            (data.totalCount === undefined || data.totalCount === null) ? (
                                                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                                    {OPERATOR_VERIFICATIONS_COPY.verificationsTotalCountOmittedNote}
                                                </Typography.Paragraph>
                                            ) : null}
                                        </>
                                    ) : (
                                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                            {OPERATOR_VERIFICATIONS_COPY.verificationsClientPaginationNote}
                                        </Typography.Paragraph>
                                    )}
                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                        {OPERATOR_VERIFICATIONS_COPY.keywordSampleFootnote}
                                    </Typography.Paragraph>
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        <strong>Vertragsgrenze:</strong>{' '}
                                        {RKSv_ADMIN_CONTRACT_GAPS.verificationsAuditVsSignatureDebug}{' '}
                                        {RKSv_ADMIN_CONTRACT_GAPS.receiptSignatureDebugResponse}
                                    </Typography.Text>
                                </Space>
                            ),
                        },
                    ]}
                />

                <Space direction="horizontal" wrap style={{ marginBottom: 12 }}>
                    <Space direction="horizontal">
                        <Typography.Text>Offline-Ursprung</Typography.Text>
                        <Tooltip title={OPERATOR_VERIFICATIONS_COPY.filterSwitchClientTooltip}>
                            <Switch checked={offlineOriginOnly} onChange={setOfflineOriginOnly} />
                        </Tooltip>
                    </Space>
                    <Space direction="horizontal">
                        <Typography.Text>Fehlerhafte Replays</Typography.Text>
                        <Tooltip title={OPERATOR_VERIFICATIONS_COPY.filterSwitchClientTooltip}>
                            <Switch checked={failedReplayOnly} onChange={setFailedReplayOnly} />
                        </Tooltip>
                    </Space>
                    <Space direction="horizontal">
                        <Typography.Text>Verdächtige Offline-Zeit</Typography.Text>
                        <Tooltip title={OPERATOR_VERIFICATIONS_COPY.filterSwitchClientTooltip}>
                            <Switch checked={suspiciousTimingOnly} onChange={setSuspiciousTimingOnly} />
                        </Tooltip>
                    </Space>
                </Space>
                {!isLoadingList && apiRows > 0 && displayedRows === 0 ? (
                    <Alert
                        type="warning"
                        showIcon
                        style={{ marginBottom: 12 }}
                        message={OPERATOR_VERIFICATIONS_COPY.verificationsNoRowsAfterFiltersTitle}
                        description={OPERATOR_VERIFICATIONS_COPY.verificationsNoRowsAfterFiltersBody(apiRows)}
                    />
                ) : null}
                {!isLoadingList &&
                apiRows > 0 &&
                displayedRows > 0 &&
                (keywordRows < apiRows || displayedRows < keywordRows) ? (
                    <div
                        style={{
                            marginBottom: 10,
                            paddingLeft: 10,
                            borderLeft: '3px solid rgba(0, 0, 0, 0.12)',
                        }}
                    >
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            <Typography.Text strong style={{ fontSize: 12 }}>
                                {OPERATOR_VERIFICATIONS_COPY.verificationsPartialScopeAlertTitle}:{' '}
                            </Typography.Text>
                            {OPERATOR_VERIFICATIONS_COPY.verificationsPartialScopeNote(
                                apiRows,
                                keywordRows,
                                displayedRows,
                            )}
                        </Typography.Text>
                    </div>
                ) : null}
                <Table
                    columns={columns}
                    dataSource={filteredEntries}
                    loading={isLoadingList}
                    rowKey={(r) => r.id ?? `${r.timestamp ?? ''}-${r.action ?? ''}-${r.entityId ?? ''}`}
                    pagination={tablePagination}
                    size="small"
                    sticky
                    scroll={{ x: 1480, y: 'calc(100vh - 420px)' }}
                    expandable={{ expandedRowRender: renderExpandedAuditRow }}
                    locale={{
                        emptyText:
                            apiRows === 0
                                ? useCorrelation
                                    ? OPERATOR_VERIFICATIONS_COPY.verificationsTableEmptyCorrelation
                                    : OPERATOR_VERIFICATIONS_COPY.verificationsTableEmptyNoRawRows
                                : OPERATOR_VERIFICATIONS_COPY.verificationsTableEmptyAllFiltered,
                    }}
                />
            </Card>
        </>
    );
}
