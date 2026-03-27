'use client';

/**
 * FinanzOnline Reconciliation — mixed surface: investigation (list, filters, metrics, drill-down context)
 * plus remediation (POST retry per payment). Operational truth is read path; mutations are isolated in row actions.
 */

import React, { useCallback, useMemo, useState } from 'react';
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
    Collapse,
    Divider,
} from 'antd';
import { ReloadOutlined, SyncOutlined } from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import dayjs, { type Dayjs } from 'dayjs';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_NAV_LABEL_KEYS, ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import {
    parseAuthoritativePaymentGuid,
    parseAuthoritativeRegisterGuid,
    toLinkSafeRegisterRowId,
} from '@/shared/utils/registerIdentity';
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
    isFinanzOnlineRetryButtonContract,
} from '@/shared/foReconciliationRowTriage';
import { finanzOnlineRowTechnicalResponseSummary } from '@/shared/finanzOnlineReconciliationTruth';
import {
    OPERATOR_FO_OPERATIONS_PAGE_COPY,
    OPERATOR_FO_QUEUE_COPY,
    OPERATOR_INVESTIGATION_CONTEXT_COPY,
    OPERATOR_LINK_LABELS,
    OPERATOR_SHARED_COPY,
} from '@/shared/operatorTruthCopy';

const STATUS_OPTIONS: { value: string; label: string }[] = [
    { value: 'Pending', label: 'Ausstehend (Pending)' },
    { value: 'Failed', label: 'Fehlgeschlagen (Failed)' },
    { value: 'NeedsReconciliation', label: 'Abgleich erforderlich (NeedsReconciliation)' },
    { value: 'Submitted', label: 'Eingereicht (Submitted)' },
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
    const { t, formatLocale } = useI18n();
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
        return parseAuthoritativePaymentGuid(raw) ?? undefined;
    }, [searchParams]);

    const rejectedFocusPaymentParam = useMemo(() => {
        const raw = searchParams?.get('focusPaymentId');
        if (raw == null || raw.trim() === '') return undefined;
        return parseAuthoritativePaymentGuid(raw) ? undefined : raw.trim();
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

    const statusOptions = useMemo(
        () =>
            (['Pending', 'Failed', 'NeedsReconciliation', 'Submitted'] as const).map((value) => ({
                value,
                label: t(`finanzOnlineReconciliation.queuePage.statusOption.${value}`),
            })),
        [t],
    );

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
            const msg = result.message ?? '';
            if (result.success) {
                message.success(t('finanzOnlineReconciliation.queuePage.retry.success', { paymentId, message: msg }));
            } else {
                message.warning(t('finanzOnlineReconciliation.queuePage.retry.warning', { paymentId, message: msg }));
            }
            setRetryingId(null);
            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.metrics });
        },
        onError: (err: Error, paymentId) => {
            const msg = err?.message || t('finanzOnlineReconciliation.queuePage.retry.errorFallback');
            message.error(t('finanzOnlineReconciliation.queuePage.retry.error', { paymentId, message: msg }));
            setRetryingId(null);
            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
        },
    });

    const handleRetry = useCallback(
        (paymentId: string) => {
            setRetryingId(paymentId);
            retryMutation.mutate(paymentId);
        },
        [retryMutation],
    );

    const columns = useMemo(
        () => [
        {
            title: (
                <Tooltip title={t('finanzOnlineReconciliation.queuePage.columns.receiptNumberTooltip')}>
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.receiptNumber')}</span>
                </Tooltip>
            ),
            dataIndex: 'receiptNumber',
            key: 'receiptNumber',
            width: 160,
            render: (val: string) => (
                <Space direction="vertical" size={0}>
                    <Typography.Text code copyable>
                        {val || '—'}
                    </Typography.Text>
                    <Link href={`/receipts?receiptNumber=${encodeURIComponent(val || '')}`} target="_blank" rel="noopener noreferrer">
                        {t('finanzOnlineReconciliation.queuePage.columns.searchReceiptsLink')}
                    </Link>
                </Space>
            ),
        },
        {
            title: (
                <Tooltip title={t('finanzOnlineReconciliation.queuePage.columns.paymentTooltip')}>
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.paymentShort')}</span>
                </Tooltip>
            ),
            key: 'paymentId',
            width: 236,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const paymentId = r.paymentId ?? '';
                if (!paymentId) return <Typography.Text type="secondary">—</Typography.Text>;
                return (
                    <Space direction="vertical" size={2} style={{ maxWidth: 228 }}>
                        <Link href={`/payments?paymentId=${paymentId}`} target="_blank" rel="noopener noreferrer">
                            {t('finanzOnlineReconciliation.queuePage.columns.openInPayments')}
                        </Link>
                        <Typography.Text code copyable={{ text: paymentId }} style={{ fontSize: 11, wordBreak: 'break-all' }}>
                            {paymentId}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        {
            title: (
                <Tooltip title={OPERATOR_FO_QUEUE_COPY.foStatusColumnTooltip}>
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.foStatusShort')}</span>
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
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.foActionColumnShort')}</span>
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
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.foTimelineShort')}</span>
                </Tooltip>
            ),
            key: 'foTimeline',
            width: 148,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => (
                <Space direction="vertical" size={0}>
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        {t('finanzOnlineReconciliation.queuePage.columns.rowCreated')}{' '}
                        {r.createdAt && dayjs(r.createdAt).isValid()
                            ? formatDateTime(r.createdAt, formatLocale, { dateStyle: 'short', timeStyle: 'short' })
                            : '—'}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        {t('finanzOnlineReconciliation.queuePage.columns.attempts')} {r.finanzOnlineRetryCount ?? 0}
                    </Typography.Text>
                    <Typography.Text style={{ fontSize: 11 }}>
                        {t('finanzOnlineReconciliation.queuePage.columns.lastAttempt')}{' '}
                        {r.finanzOnlineLastAttemptAtUtc && dayjs(r.finanzOnlineLastAttemptAtUtc).isValid()
                            ? formatDateTime(r.finanzOnlineLastAttemptAtUtc, formatLocale, {
                                  dateStyle: 'short',
                                  timeStyle: 'short',
                              })
                            : '—'}
                    </Typography.Text>
                </Space>
            ),
        },
        {
            title: (
                <Tooltip title={OPERATOR_FO_QUEUE_COPY.foErrorShortTooltip}>
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.errorShort')}</span>
                </Tooltip>
            ),
            key: 'finanzOnlineError',
            width: 220,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const summary = finanzOnlineRowTechnicalResponseSummary(r);
                return summary ? (
                    <Typography.Paragraph
                        type="danger"
                        style={{ marginBottom: 0, maxWidth: 212, fontSize: 12, lineHeight: 1.35 }}
                        ellipsis={{ rows: 2, tooltip: summary }}
                    >
                        {summary}
                    </Typography.Paragraph>
                ) : (
                    '—'
                );
            },
        },
        {
            title: (
                <Tooltip title="finanzOnlineReferenceId — FinanzOnline-Referenz aus dem Listen-DTO.">
                    <span>Referenz (FO)</span>
                </Tooltip>
            ),
            dataIndex: 'finanzOnlineReferenceId',
            key: 'finanzOnlineReferenceId',
            width: 120,
            ellipsis: true,
            render: (v: string | null) => (v ? <Typography.Text code copyable>{v}</Typography.Text> : '—'),
        },
        {
            title: (
                <Tooltip title={t('finanzOnlineReconciliation.queuePage.columns.amountTooltip')}>
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.amountShort')}</span>
                </Tooltip>
            ),
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            width: 90,
            render: (v: number) =>
                v != null ? formatCurrency(Number(v), formatLocale) : '—',
        },
        {
            title: (
                <Tooltip title={adminTruthTooltip('authoritative_api')}>
                    <span>{t('finanzOnlineReconciliation.queuePage.columns.registerFkShort')}</span>
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
                            style={{ fontSize: 11, wordBreak: 'break-all', maxWidth: 200 }}
                        >
                            {v.apiCashRegisterId}
                        </Typography.Text>
                        <AdminTruthBadge
                            kind={registerDeepLinkEligibleBadgeKind({
                                linkSafeUuid: v.finanzQueueRegisterRowId,
                            })}
                        />
                        {v.registerFkRawNotLinkSafe ? (
                            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                {t('finanzOnlineReconciliation.queuePage.columns.rawRegisterVisible')}
                            </Typography.Text>
                        ) : null}
                    </Space>
                );
            },
        },
        {
            title: t('finanzOnlineReconciliation.queuePage.columns.actions'),
            key: 'actions',
            width: 100,
            fixed: 'right' as const,
            render: (_: unknown, r: FinanzOnlineReconciliationItemDto) => {
                const canRetry = isFinanzOnlineRetryButtonContract(r);
                const paymentId = r.paymentId ?? '';
                const loading = retryingId === paymentId;
                return canRetry && paymentId ? (
                    <Tooltip title={OPERATOR_FO_QUEUE_COPY.retryActionButtonTooltip}>
                        <Button
                            type="link"
                            size="small"
                            icon={<SyncOutlined />}
                            loading={loading}
                            onClick={() => handleRetry(paymentId)}
                        >
                            {t('finanzOnlineReconciliation.queuePage.columns.retrySend')}
                        </Button>
                    </Tooltip>
                ) : null;
            },
        },
        ],
        [t, formatLocale, retryingId, handleRetry],
    );

    const isLoading = listLoading || metricsLoading;
    const items = listData?.items ?? [];

    const hasUrlParamRejections = Boolean(rejectedRegisterQueryParam || rejectedFocusPaymentParam);
    const hasInvestigationUrlContext = Boolean(investigationBatchCorrelationId || focusPaymentId);

    return (
        <>
            <AdminPageHeader
                title={
                    <Space align="center" size="middle" wrap>
                        <span>{t(ADMIN_NAV_LABEL_KEYS.finanzOnlineAbgleichLegacy)}</span>
                        <Tag color="orange">{t('finanzOnlineReconciliation.legacyBadge')}</Tag>
                    </Space>
                }
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: t(ADMIN_NAV_LABEL_KEYS.finanzOnlineAbgleichLegacy) },
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
                            {OPERATOR_SHARED_COPY.toolbarRefresh}
                        </Button>
                    </Tooltip>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 6, fontSize: 12, maxWidth: 900 }}>
                    {OPERATOR_FO_QUEUE_COPY.pageLeadCompact}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12, maxWidth: 900 }}>
                    {OPERATOR_FO_QUEUE_COPY.relatedSupportingLabel}:{' '}
                    <Link href="/rksv/finanz-online-operations">
                        {OPERATOR_FO_OPERATIONS_PAGE_COPY.breadcrumbTitle}
                    </Link>
                    {' · '}
                    <Link href="/rksv/integrity">
                        {t('finanzOnlineReconciliation.queuePage.relatedLinks.integritySupport')}
                    </Link>
                    {' · '}
                    <Link href="/rksv/incident">
                        {t('finanzOnlineReconciliation.queuePage.relatedLinks.incidentCorrelation')}
                    </Link>
                    {' · '}
                    <Link href="/payments">{ADMIN_NAV_LABELS.payments}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                message={t('finanzOnlineReconciliation.outboxPrimaryBanner.title')}
                description={
                    <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                        {t('finanzOnlineReconciliation.outboxPrimaryBanner.leadBeforeLink')}{' '}
                        <Link href="/rksv/finanz-online-outbox">{t('nav.finanzOnlineOutbox')}</Link>
                        {t('finanzOnlineReconciliation.outboxPrimaryBanner.leadAfterLink')}
                    </Typography.Paragraph>
                }
            />

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 12 }}
                message={t('finanzOnlineReconciliation.phasedDeprecationNote')}
            />

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message={OPERATOR_FO_QUEUE_COPY.pageTopDisclaimerMessage}
                description={
                    <Space direction="vertical" size={8} style={{ width: '100%' }}>
                        <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                            {OPERATOR_FO_QUEUE_COPY.pageTopDisclaimerLead}
                        </Typography.Paragraph>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                            {OPERATOR_FO_QUEUE_COPY.pageTopDisclaimerUrlContext}
                        </Typography.Paragraph>
                    </Space>
                }
            />

            <Collapse
                bordered={false}
                ghost
                size="small"
                style={{ marginBottom: 12 }}
                items={[
                    {
                        key: 'listen-kontext',
                        label: OPERATOR_FO_QUEUE_COPY.foQueueListenKontextCollapseTitle,
                        children: (
                            <Space direction="vertical" size="small" style={{ width: '100%' }}>
                                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                    {OPERATOR_FO_QUEUE_COPY.pagePrimaryOperationalTruthLead}
                                </Typography.Paragraph>
                            </Space>
                        ),
                    },
                ]}
            />

            {listError ? (
                <Alert
                    type="error"
                    message={OPERATOR_SHARED_COPY.loadFailedList}
                    description={
                        listError instanceof Error ? listError.message : OPERATOR_SHARED_COPY.unknownErrorDetail
                    }
                    style={{ marginBottom: 12 }}
                    showIcon
                    action={
                        <Button
                            size="small"
                            onClick={() => {
                                queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
                                queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.metrics });
                            }}
                        >
                            {OPERATOR_SHARED_COPY.retryAfterError}
                        </Button>
                    }
                />
            ) : null}

            {hasUrlParamRejections ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 12 }}
                    message={OPERATOR_FO_QUEUE_COPY.urlParamRejectedCombinedTitle}
                    description={
                        <Space direction="vertical" size={6} style={{ width: '100%' }}>
                            {rejectedRegisterQueryParam ? (
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    <strong>{OPERATOR_FO_QUEUE_COPY.queryRejectedRegisterTitle}:</strong>{' '}
                                    {OPERATOR_FO_QUEUE_COPY.queryRejectedRegisterDescription(
                                        rejectedRegisterQueryParam,
                                    )}
                                </Typography.Text>
                            ) : null}
                            {rejectedFocusPaymentParam ? (
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    <strong>{OPERATOR_FO_QUEUE_COPY.queryRejectedFocusPaymentTitle}:</strong>{' '}
                                    {OPERATOR_FO_QUEUE_COPY.queryRejectedFocusPaymentDescription(
                                        rejectedFocusPaymentParam,
                                    )}
                                </Typography.Text>
                            ) : null}
                        </Space>
                    }
                />
            ) : null}

            {hasInvestigationUrlContext ? (
                <Card
                    size="small"
                    style={{ marginBottom: 12 }}
                    title={
                        investigationBatchCorrelationId
                            ? OPERATOR_INVESTIGATION_CONTEXT_COPY.bannerTitle
                            : OPERATOR_INVESTIGATION_CONTEXT_COPY.focusPaymentOnlyTitle
                    }
                >
                    <Space direction="vertical" size={10} style={{ width: '100%' }}>
                        {investigationBatchCorrelationId ? (
                            <>
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
                                {focusPaymentId ? (
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {OPERATOR_INVESTIGATION_CONTEXT_COPY.focusPaymentLine}{' '}
                                        <Typography.Text code>{focusPaymentId}</Typography.Text>
                                    </Typography.Text>
                                ) : null}
                                <Collapse
                                    bordered={false}
                                    ghost
                                    size="small"
                                    items={[
                                        {
                                            key: 'inv-full',
                                            label: OPERATOR_FO_QUEUE_COPY.investigationUrlContextCollapseTitle,
                                            children: (
                                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                    {OPERATOR_INVESTIGATION_CONTEXT_COPY.bannerBody}
                                                </Typography.Text>
                                            ),
                                        },
                                    ]}
                                />
                            </>
                        ) : (
                            <Space direction="vertical" size={6} style={{ width: '100%' }}>
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {OPERATOR_INVESTIGATION_CONTEXT_COPY.focusPaymentOnlyBody}
                                </Typography.Text>
                                {focusPaymentId ? (
                                    <Typography.Text code copyable>
                                        {focusPaymentId}
                                    </Typography.Text>
                                ) : null}
                            </Space>
                        )}
                    </Space>
                </Card>
            ) : null}

            <OperatorSummaryStrip>
                <Space direction="vertical" size={10} style={{ width: '100%', marginBottom: 8 }}>
                    <Space wrap align="center">
                        <Tag color="geekblue">{OPERATOR_FO_QUEUE_COPY.metricsAggregatedBadge}</Tag>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {OPERATOR_FO_QUEUE_COPY.metricsAggregatedFootnote}
                        </Typography.Text>
                    </Space>
                </Space>
                <Row gutter={[16, 16]}>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('finanzOnlineReconciliation.queuePage.metrics.submitTotalRun')}
                                value={metricsData?.submitTotal ?? 0}
                                loading={metricsLoading}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('finanzOnlineReconciliation.queuePage.metrics.failedTotal')}
                                value={metricsData?.submitFailedTotal ?? 0}
                                loading={metricsLoading}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('finanzOnlineReconciliation.queuePage.metrics.transientMetric')}
                                value={metricsData?.submitFailedTransient ?? 0}
                                loading={metricsLoading}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('finanzOnlineReconciliation.queuePage.metrics.permanentMetric')}
                                value={metricsData?.submitFailedPermanent ?? 0}
                                loading={metricsLoading}
                            />
                        </Card>
                    </Col>
                </Row>
                <Collapse
                    bordered={false}
                    ghost
                    size="small"
                    style={{ marginTop: 10 }}
                    items={[
                        {
                            key: 'metric-hints',
                            label: OPERATOR_FO_QUEUE_COPY.foQueueMetricsHintsCollapseTitle,
                            children: (
                                <Space direction="vertical" size="small" style={{ width: '100%' }}>
                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                        {OPERATOR_FO_QUEUE_COPY.summaryReconciliationParagraph}
                                    </Typography.Paragraph>
                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                        {OPERATOR_FO_QUEUE_COPY.metricsFailureKindScope}
                                    </Typography.Paragraph>
                                </Space>
                            ),
                        },
                    ]}
                />
            </OperatorSummaryStrip>

            <OperatorBusinessSection
                title={OPERATOR_FO_QUEUE_COPY.businessSectionTitle}
                description={OPERATOR_FO_QUEUE_COPY.businessSectionDescriptionCompact}
            >
                <Collapse
                    bordered={false}
                    ghost
                    size="small"
                    style={{ marginBottom: 12 }}
                    items={[
                        {
                            key: 'business-full',
                            label: OPERATOR_FO_QUEUE_COPY.businessSectionDescriptionCollapseTitle,
                            children: (
                                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                    {OPERATOR_FO_QUEUE_COPY.businessSectionDescription}
                                </Typography.Paragraph>
                            ),
                        },
                    ]}
                />
            <Card title={t('finanzOnlineReconciliation.queuePage.filters.cardTitle')} size="small" style={{ marginBottom: 16 }}>
                <Space wrap size="middle">
                    <Space>
                        <Typography.Text strong>{t('finanzOnlineReconciliation.queuePage.filters.statusLabel')}</Typography.Text>
                        <Select
                            mode="multiple"
                            placeholder={t('finanzOnlineReconciliation.queuePage.filters.statusPlaceholder')}
                            value={statusFilter}
                            onChange={(v) => setStatusFilter(v ?? [])}
                            options={statusOptions}
                            style={{ minWidth: 260 }}
                        />
                    </Space>
                    <Space>
                        <Typography.Text strong>{t('finanzOnlineReconciliation.queuePage.filters.registerLabel')}</Typography.Text>
                        <Select
                            placeholder={t('finanzOnlineReconciliation.queuePage.filters.registerPlaceholderAll')}
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
                        <Typography.Text strong>{t('finanzOnlineReconciliation.queuePage.filters.utcRangeLabel')}</Typography.Text>
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
                    <>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
                            {OPERATOR_FO_QUEUE_COPY.tableExpandDiscoverabilityHint}
                        </Typography.Paragraph>
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
                            showTotal: (total) =>
                                t('finanzOnlineReconciliation.queuePage.table.paginationTotal', { total }),
                        }}
                        size="small"
                        scroll={{ x: 1620 }}
                        expandable={{
                            expandedRowRender: (record) => {
                                const reg = viewFinanzReconciliationRegister(record);
                                const pid = record.paymentId?.trim() ?? '';
                                const syncHref = buildFinanzOnlineQueueInvestigationHref({
                                    registerRowId: toLinkSafeRegisterRowId(cashRegisterId),
                                    focusPaymentId,
                                    investigationBatchCorrelationId,
                                    fromUtc: dateRange[0]?.toISOString(),
                                    toUtc: dateRange[1]?.endOf('day').toISOString(),
                                    statusCsv: statusFilter.length ? statusFilter.join(',') : undefined,
                                });
                                return (
                                    <div style={{ padding: '8px 12px 16px', background: '#fafafa' }}>
                                        <Typography.Text strong style={{ fontSize: 12, display: 'block' }}>
                                            Zeile — technische Details (Listen-API)
                                        </Typography.Text>

                                        <Divider orientation="left" plain style={{ margin: '12px 0 8px' }}>
                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                {OPERATOR_FO_QUEUE_COPY.expandSectionErrorTitle}
                                            </Typography.Text>
                                        </Divider>
                                        <Descriptions bordered size="small" column={1}>
                                            <Descriptions.Item label="finanzOnlineError">
                                                {record.finanzOnlineError?.trim() ? (
                                                    <Typography.Text type="danger" copyable>
                                                        {record.finanzOnlineError}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                        </Descriptions>

                                        <Divider orientation="left" plain style={{ margin: '16px 0 8px' }}>
                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                {OPERATOR_FO_QUEUE_COPY.expandSectionIdentifiersTitle}
                                            </Typography.Text>
                                        </Divider>
                                        <Descriptions bordered size="small" column={1}>
                                            <Descriptions.Item label="paymentId">
                                                {pid ? (
                                                    <Typography.Text code copyable>
                                                        {pid}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="receiptNumber">
                                                {record.receiptNumber?.trim() ? (
                                                    <Typography.Text code copyable>
                                                        {record.receiptNumber}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="cashRegisterId">
                                                {reg.apiCashRegisterId ? (
                                                    <Typography.Text code copyable>
                                                        {reg.apiCashRegisterId}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="finanzOnlineReferenceId">
                                                {record.finanzOnlineReferenceId?.trim() ? (
                                                    <Typography.Text code copyable>
                                                        {record.finanzOnlineReferenceId}
                                                    </Typography.Text>
                                                ) : (
                                                    '—'
                                                )}
                                            </Descriptions.Item>
                                        </Descriptions>

                                        <Divider orientation="left" plain style={{ margin: '16px 0 8px' }}>
                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                {OPERATOR_FO_QUEUE_COPY.expandSectionTimestampsTitle}
                                            </Typography.Text>
                                        </Divider>
                                        <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 11 }}>
                                            {OPERATOR_FO_QUEUE_COPY.expandTimestampsUtcHint}
                                        </Typography.Paragraph>
                                        <Descriptions bordered size="small" column={1}>
                                            <Descriptions.Item label="createdAt">
                                                {record.createdAt && dayjs(record.createdAt).isValid()
                                                    ? dayjs(record.createdAt).format('DD.MM.YYYY HH:mm:ss')
                                                    : '—'}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="finanzOnlineLastAttemptAtUtc">
                                                {record.finanzOnlineLastAttemptAtUtc &&
                                                dayjs(record.finanzOnlineLastAttemptAtUtc).isValid()
                                                    ? dayjs(record.finanzOnlineLastAttemptAtUtc).format(
                                                          'DD.MM.YYYY HH:mm:ss',
                                                      )
                                                    : '—'}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="finanzOnlineRetryCount">
                                                {record.finanzOnlineRetryCount ?? 0}
                                            </Descriptions.Item>
                                        </Descriptions>

                                        <Divider orientation="left" plain style={{ margin: '16px 0 8px' }}>
                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                {OPERATOR_FO_QUEUE_COPY.expandSectionInvestigationTitle}
                                            </Typography.Text>
                                        </Divider>
                                        <Space direction="vertical" size={8} style={{ width: '100%' }}>
                                            {pid ? (
                                                <Link
                                                    href={`/payments?paymentId=${encodeURIComponent(pid)}`}
                                                    target="_blank"
                                                    rel="noopener noreferrer"
                                                >
                                                    {t('finanzOnlineReconciliation.queuePage.expandRow.openPaymentInPayments')}
                                                </Link>
                                            ) : null}
                                            {record.receiptNumber?.trim() ? (
                                                <Link
                                                    href={`/receipts?receiptNumber=${encodeURIComponent(record.receiptNumber)}`}
                                                    target="_blank"
                                                    rel="noopener noreferrer"
                                                >
                                                    {t('finanzOnlineReconciliation.queuePage.expandRow.searchReceiptsByNumber')}
                                                </Link>
                                            ) : null}
                                            {investigationBatchCorrelationId ? (
                                                <>
                                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                                        {OPERATOR_FO_QUEUE_COPY.expandInvestigationWithUrlCorrelationHint}
                                                    </Typography.Paragraph>
                                                    <Space wrap>
                                                        <Link
                                                            href={buildIncidentInvestigationHref(
                                                                investigationBatchCorrelationId,
                                                            )}
                                                        >
                                                            {OPERATOR_LINK_LABELS.incidentAggregate}
                                                        </Link>
                                                        <Typography.Text type="secondary">·</Typography.Text>
                                                        <Link
                                                            href={buildReplayBatchDetailHref(
                                                                investigationBatchCorrelationId,
                                                            )}
                                                        >
                                                            {OPERATOR_LINK_LABELS.replayBatchDetail}
                                                        </Link>
                                                        <Typography.Text type="secondary">·</Typography.Text>
                                                        <Link href={syncHref}>
                                                            {OPERATOR_INVESTIGATION_CONTEXT_COPY.syncUrlWithFiltersLink}
                                                        </Link>
                                                    </Space>
                                                </>
                                            ) : (
                                                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                                                    {OPERATOR_FO_QUEUE_COPY.expandInvestigationNoUrlCorrelation}
                                                </Typography.Paragraph>
                                            )}
                                        </Space>

                                        <Typography.Text strong style={{ fontSize: 12, display: 'block', marginTop: 16 }}>
                                            {OPERATOR_FO_QUEUE_COPY.contractTruthPanelTitle}
                                        </Typography.Text>
                                        <Descriptions bordered size="small" column={1} style={{ marginTop: 8 }}>
                                            <Descriptions.Item label={OPERATOR_FO_QUEUE_COPY.contractTruthInDtoTitle}>
                                                <ul style={{ margin: 0, paddingLeft: 18, fontSize: 12 }}>
                                                    {OPERATOR_FO_QUEUE_COPY.contractTruthInDtoBullets.map((line) => (
                                                        <li key={line}>
                                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                                {line}
                                                            </Typography.Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            </Descriptions.Item>
                                            <Descriptions.Item label={OPERATOR_FO_QUEUE_COPY.contractTruthNotInDtoTitle}>
                                                <ul style={{ margin: 0, paddingLeft: 18, fontSize: 12 }}>
                                                    {OPERATOR_FO_QUEUE_COPY.contractTruthNotInDtoBullets.map((line) => (
                                                        <li key={line}>
                                                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                                {line}
                                                            </Typography.Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            </Descriptions.Item>
                                        </Descriptions>
                                    </div>
                                );
                            },
                        }}
                    />
                    </>
                )}
            </Card>
            </OperatorBusinessSection>
        </>
    );
}
