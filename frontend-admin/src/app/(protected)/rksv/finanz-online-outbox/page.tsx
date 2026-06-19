'use client';

/**
 * FinanzOnline Outbox — operative Sicht auf die SOAP/Outbox-Pipeline (kein Payment-Zeilen-Legacy).
 * Liest nur freigegebene Felder; keine Roh-XML/Credentials (Backend redacted).
 */

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    DatePicker,
    Descriptions,
    Divider,
    Drawer,
    Empty,
    Input,
    Select,
    Space,
    Spin,
    Table,
    Tag,
    Tooltip,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
    getApiAdminFinanzonlineOutbox,
    getApiAdminFinanzonlineOutboxId,
    getApiAdminFinanzonlineReadiness,
} from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { FinanzOnlineOutboxItemDto } from '@/api/generated/model/finanzOnlineOutboxItemDto';
import type { GetApiAdminFinanzonlineOutboxParams } from '@/api/generated/model/getApiAdminFinanzonlineOutboxParams';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import {
    FINANZ_ONLINE_TRANSPORT_PATH_KIND,
    finanzOnlineOutboxStatusTagColor,
    finanzOnlineTransportPathTagColor,
    isSimulatedFinanzOnlineTransportPath,
    labelFinanzOnlineTransportPathKind,
} from '@/shared/finanzOnlineTransportPathPresentation';
import { finanzOnlineReadinessFindingGermanTitle } from '@/shared/finanzOnlineReadinessFindingPresentation';
import { parseAuthoritativePaymentGuid } from '@/shared/utils/registerIdentity';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

dayjs.extend(utc);

function readinessOverallTagColor(
    status: string | null | undefined
): 'success' | 'warning' | 'error' | 'default' {
    const u = (status ?? '').toLowerCase();
    if (u === 'healthy') return 'success';
    if (u === 'unhealthy') return 'error';
    return 'warning';
}

function protocolSuccessStatusHintKey(transportPathKind: string | null | undefined): string | null {
    if (transportPathKind === FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated) {
        return 'finanzOnlineOutbox.table.protocolSuccess.simulatedCaution';
    }
    if (transportPathKind === FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealTest) {
        return 'finanzOnlineOutbox.table.protocolSuccess.realTestNote';
    }
    if (transportPathKind === FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealProduction) {
        return 'finanzOnlineOutbox.table.protocolSuccess.realProductionNote';
    }
    return null;
}

function readinessFindingAlertType(
    severity: string | null | undefined
): 'error' | 'warning' | 'info' {
    const u = (severity ?? '').toLowerCase();
    if (u === 'error') return 'error';
    if (u === 'warning') return 'warning';
    return 'info';
}

export default function FinanzOnlineOutboxPage() {
    const { t } = useI18n();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();
    const autoOpenedOutboxIdRef = useRef<string | null>(null);
    const [bucket, setBucket] = useState<string>('all');
    const [statusCsv, setStatusCsv] = useState<string>('');
    const [mode, setMode] = useState<string | undefined>(undefined);
    const [correlationId, setCorrelationId] = useState<string>('');
    const [businessKey, setBusinessKey] = useState<string>('');
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([null, null]);
    const [drawerOpen, setDrawerOpen] = useState(false);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [selectedRow, setSelectedRow] = useState<FinanzOnlineOutboxItemDto | null>(null);

    const urlOutboxId = useMemo(() => {
        const raw = searchParams?.get('outboxId')?.trim();
        return parseAuthoritativePaymentGuid(raw);
    }, [searchParams]);

    const emDash = useMemo(() => t('finanzOnlineOutbox.format.emptyValue'), [t]);
    const utcSuffix = useMemo(() => t('finanzOnlineOutbox.format.utcSuffix'), [t]);

    const fmtUtc = useCallback(
        (iso: string | undefined | null) => {
            if (!iso) return emDash;
            const d = dayjs(iso);
            return d.isValid() ? d.utc().format('YYYY-MM-DD HH:mm:ss') + utcSuffix : emDash;
        },
        [emDash, utcSuffix]
    );

    const modeTag = useCallback(
        (modeVal: string | null | undefined) => {
            const m = (modeVal ?? 'TEST').toUpperCase();
            return <Tag color={m === 'PROD' ? 'red' : 'blue'}>{m}</Tag>;
        },
        []
    );

    const transportPathTag = useCallback(
        (kind: string | null | undefined) => {
            if (!kind?.trim()) {
                return <Typography.Text type="secondary">{emDash}</Typography.Text>;
            }
            const text = labelFinanzOnlineTransportPathKind((k) => t(k), kind, emDash);
            return (
                <Tooltip title={kind}>
                    <Tag color={finanzOnlineTransportPathTagColor(kind)}>{text}</Tag>
                </Tooltip>
            );
        },
        [t, emDash]
    );

    const bucketOptions = useMemo(
        () => [
            { value: 'all', label: t('finanzOnlineOutbox.buckets.all') },
            { value: 'in_flight', label: t('finanzOnlineOutbox.buckets.inFlight') },
            { value: 'pending', label: t('finanzOnlineOutbox.buckets.pending') },
            { value: 'processing', label: t('finanzOnlineOutbox.buckets.processing') },
            { value: 'AwaitingProtocol', label: t('finanzOnlineOutbox.buckets.awaitingProtocol') },
            { value: 'RetryableFailure', label: t('finanzOnlineOutbox.buckets.retryableFailure') },
            { value: 'PermanentFailure', label: t('finanzOnlineOutbox.buckets.permanentFailure') },
            { value: 'ProtocolFailure', label: t('finanzOnlineOutbox.buckets.protocolFailure') },
            { value: 'ManualReviewRequired', label: t('finanzOnlineOutbox.buckets.manualReviewRequired') },
            { value: 'ProtocolSuccess', label: t('finanzOnlineOutbox.buckets.protocolSuccess') },
            { value: 'dead_letter', label: t('finanzOnlineOutbox.buckets.deadLetter') },
        ],
        [t]
    );

    const listParams: GetApiAdminFinanzonlineOutboxParams = useMemo(() => {
        const p: GetApiAdminFinanzonlineOutboxParams = { limit: 200 };
        const trimmedStatus = statusCsv.trim();
        if (trimmedStatus) {
            p.status = trimmedStatus;
        } else if (bucket && bucket !== 'all') {
            p.bucket = bucket;
        }
        const m = mode?.trim();
        if (m) p.mode = m;
        const c = correlationId.trim();
        if (c) p.correlationId = c;
        const bk = businessKey.trim();
        if (bk) p.businessKey = bk;
        if (dateRange[0]) p.fromUtc = dateRange[0].startOf('day').toISOString();
        if (dateRange[1]) p.toUtc = dateRange[1].endOf('day').toISOString();
        if (urlOutboxId) p.outboxId = urlOutboxId;
        return p;
    }, [bucket, statusCsv, mode, correlationId, businessKey, dateRange, urlOutboxId]);

    useEffect(() => {
        if (!urlOutboxId) {
            autoOpenedOutboxIdRef.current = null;
            return;
        }
        if (autoOpenedOutboxIdRef.current === urlOutboxId) return;
        autoOpenedOutboxIdRef.current = urlOutboxId;
        setSelectedRow(null);
        setSelectedId(urlOutboxId);
        setDrawerOpen(true);
    }, [urlOutboxId]);

    const {
        data: listData,
        isLoading,
        isFetching,
        error,
    } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.list(listParams),
        queryFn: () => getApiAdminFinanzonlineOutbox(listParams),
        staleTime: 15_000,
    });

    const {
        data: readiness,
        isLoading: readinessLoading,
        isFetching: readinessFetching,
        error: readinessError,
    } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.readiness(),
        queryFn: () => getApiAdminFinanzonlineReadiness(),
        staleTime: 30_000,
    });

    const { data: detailData, isLoading: detailLoading } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.detail(selectedId ?? ''),
        queryFn: () => getApiAdminFinanzonlineOutboxId(selectedId!),
        enabled: Boolean(drawerOpen && selectedId),
        staleTime: 10_000,
    });

    const displayRow = detailData ?? selectedRow;

    const openDrawer = useCallback((row: FinanzOnlineOutboxItemDto) => {
        const id = row.outboxId;
        if (!id) return;
        setSelectedRow(row);
        setSelectedId(id);
        setDrawerOpen(true);
    }, []);

    const closeDrawer = useCallback(() => {
        setDrawerOpen(false);
        setSelectedId(null);
        setSelectedRow(null);
    }, []);

    const columns: ColumnsType<FinanzOnlineOutboxItemDto> = useMemo(
        () => [
            {
                title: t('finanzOnlineOutbox.table.columns.outboxId'),
                dataIndex: 'outboxId',
                key: 'outboxId',
                width: 280,
                ellipsis: true,
                render: (v: string) => (
                    <Typography.Text copyable={{ text: v }} style={{ fontSize: 12 }}>
                        {v}
                    </Typography.Text>
                ),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.businessKey'),
                dataIndex: 'businessKey',
                width: 160,
                ellipsis: true,
            },
            {
                title: t('finanzOnlineOutbox.table.columns.correlation'),
                dataIndex: 'correlationId',
                width: 200,
                ellipsis: true,
                render: (v: string | null | undefined) =>
                    v ? (
                        <Typography.Text copyable={{ text: v }} style={{ fontSize: 12 }}>
                            {v}
                        </Typography.Text>
                    ) : (
                        emDash
                    ),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.mode'),
                dataIndex: 'mode',
                width: 88,
                render: (_: unknown, r) => modeTag(r.mode),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.transportPath'),
                key: 'transportPathKind',
                width: 168,
                render: (_: unknown, r) => transportPathTag(r.transportPathKind),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.status'),
                key: 'status',
                width: 200,
                render: (_: unknown, r) => {
                    const hintKey =
                        r.status === 'ProtocolSuccess' ? protocolSuccessStatusHintKey(r.transportPathKind) : null;
                    return (
                        <Space orientation="vertical" size={0}>
                            <Tag color={finanzOnlineOutboxStatusTagColor(r.status, r.transportPathKind)}>
                                {r.operatorStatusLabel ?? r.status ?? emDash}
                            </Tag>
                            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                {r.status}
                            </Typography.Text>
                            {hintKey ? (
                                <Typography.Text
                                    type={isSimulatedFinanzOnlineTransportPath(r.transportPathKind) ? 'warning' : 'secondary'}
                                    style={{ fontSize: 11 }}
                                >
                                    {t(hintKey)}
                                </Typography.Text>
                            ) : null}
                        </Space>
                    );
                },
            },
            {
                title: t('finanzOnlineOutbox.table.columns.hintOrError'),
                key: 'err',
                width: 220,
                ellipsis: true,
                render: (_: unknown, r) => {
                    const hint = r.operatorFailureHint?.trim();
                    const sum = r.lastErrorSummary?.trim();
                    const sep = ' — ';
                    const line = [hint, sum].filter(Boolean).join(sep) || emDash;
                    return (
                        <Tooltip title={line}>
                            <span>{line}</span>
                        </Tooltip>
                    );
                },
            },
            {
                title: t('finanzOnlineOutbox.table.columns.attempts'),
                dataIndex: 'attemptCount',
                width: 72,
                align: 'right',
            },
            {
                title: t('finanzOnlineOutbox.table.columns.nextAttemptUtc'),
                dataIndex: 'nextAttemptAtUtc',
                width: 168,
                render: (v: string | undefined) => fmtUtc(v),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.paketTransmission'),
                dataIndex: 'transmissionId',
                width: 120,
                ellipsis: true,
            },
            {
                title: t('finanzOnlineOutbox.table.columns.protocol'),
                key: 'protocol',
                width: 220,
                ellipsis: true,
                render: (_: unknown, r) => {
                    const code = r.protocolCode?.trim();
                    const summary = r.protocolSummary?.trim();
                    if (!code && !summary) return emDash;
                    return (
                        <Space orientation="vertical" size={0}>
                            {code ? (
                                <Typography.Text code style={{ fontSize: 11 }}>
                                    {code}
                                </Typography.Text>
                            ) : null}
                            {summary ? (
                                <Typography.Text type="secondary" style={{ fontSize: 11 }} ellipsis>
                                    {summary}
                                </Typography.Text>
                            ) : null}
                        </Space>
                    );
                },
            },
            {
                title: t('finanzOnlineOutbox.table.columns.createdUtc'),
                dataIndex: 'createdAtUtc',
                width: 168,
                render: (v: string | undefined) => fmtUtc(v),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.processedUtc'),
                dataIndex: 'processedAtUtc',
                width: 168,
                render: (v: string | undefined) => fmtUtc(v),
            },
        ],
        [t, emDash, fmtUtc, modeTag, transportPathTag]
    );

    const items = listData?.items ?? [];
    const total = listData?.total ?? 0;

    const fmtReadinessBool = useCallback(
        (v: boolean | undefined | null) => {
            if (v === undefined || v === null) return emDash;
            return v ? t('finanzOnlineOutbox.readiness.yes') : t('finanzOnlineOutbox.readiness.no');
        },
        [emDash, t]
    );

    const outboxCountEntries = useMemo(() => {
        const c = readiness?.outboxCountsByStatus;
        if (!c || typeof c !== 'object') return [];
        return Object.entries(c).sort(([a], [b]) => a.localeCompare(b));
    }, [readiness?.outboxCountsByStatus]);

    const pageTitle = t(ADMIN_NAV_LABEL_KEYS.finanzOnlineOutbox);

    return (
        <>
            <AdminPageHeader
                title={pageTitle}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('adminShell.group.rksv'), href: '/rksv' },
                    { title: pageTitle },
                ]}
                actions={
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() => {
                            void queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.base });
                            void queryClient.invalidateQueries({
                                queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.readiness(),
                            });
                        }}
                    >
                        {t('common.buttons.refresh')}
                    </Button>
                }
            >
                <Typography.Paragraph style={{ marginBottom: 8, maxWidth: 960 }}>
                    <Tag color="green">{t('finanzOnlineOutbox.page.leadTag')}</Tag>{' '}
                    {t('finanzOnlineOutbox.page.leadLine')} {t('finanzOnlineOutbox.page.leadNoPaymentRows')}{' '}
                    <Link href="/rksv/finanz-online-queue">{t('finanzOnlineOutbox.page.leadLegacyQueueLink')}</Link>.
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 960, fontSize: 12 }}>
                    {t('finanzOnlineOutbox.page.authoritativeLine')}
                </Typography.Paragraph>
            </AdminPageHeader>

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                title={t('finanzOnlineOutbox.privacyAlert.title')}
                description={t('finanzOnlineOutbox.privacyAlert.description')}
            />

            {listData?.finanzOnlineTransportSimulationActive === true ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={t('finanzOnlineOutbox.transportPath.bannerSimulated')}
                    description={
                        listData?.finanzOnlineDeveloperSimulationProfile
                            ? t('finanzOnlineOutbox.transportPath.developerScenario', {
                                  profile: listData.finanzOnlineDeveloperSimulationProfile,
                              })
                            : undefined
                    }
                />
            ) : listData?.finanzOnlineTransportSimulationActive === false ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={t('finanzOnlineOutbox.transportPath.bannerReal')}
                />
            ) : null}

            <Card
                size="small"
                style={{ marginBottom: 16 }}
                title={t('finanzOnlineOutbox.readiness.cardTitle')}
            >
                <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                    {t('finanzOnlineOutbox.readiness.cardSubtitle')}
                </Typography.Paragraph>
                <Spin spinning={readinessLoading || readinessFetching}>
                    {readinessError ? (
                        <Alert
                            type="error"
                            showIcon
                            title={t('finanzOnlineOutbox.readiness.loadError')}
                            description={
                                <ApiErrorAlertDescription
                                    t={t}
                                    error={readinessError}
                                    logContext="FinanzOnlineOutbox.readiness"
                                    fallbackKey="finanzOnlineOutbox.readiness.loadError"
                                />
                            }
                        />
                    ) : readiness ? (
                        <>
                            <Space wrap size="middle" align="center" style={{ marginBottom: 12 }}>
                                <Typography.Text type="secondary">
                                    {t('finanzOnlineOutbox.readiness.overall')}:
                                </Typography.Text>
                                <Tag color={readinessOverallTagColor(readiness.overallStatus)}>
                                    {readiness.overallStatus ?? emDash}
                                </Tag>
                                <Typography.Text type="secondary">
                                    {t('finanzOnlineOutbox.readiness.transportMode')}:
                                </Typography.Text>
                                <Tag>{readiness.transportMode ?? emDash}</Tag>
                            </Space>
                            <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.realTestSubmit')}>
                                    {fmtReadinessBool(readiness.realTestSubmissionPossible)}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.protocol')}>
                                    {fmtReadinessBool(readiness.protocolReconciliationPossible)}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.outboxWorker')}>
                                    {fmtReadinessBool(readiness.outboxWorkerEnabled)}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.scenarioConfigured')}>
                                    {readiness.configuredSimulationScenario?.trim() || emDash}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.scenarioEffective')}>
                                    {readiness.effectiveSimulationScenario?.trim() || emDash}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.scenarioDelay')}>
                                    {readiness.simulationFixedDelayMs != null ? String(readiness.simulationFixedDelayMs) : emDash}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('finanzOnlineOutbox.readiness.scenarioSeed')}>
                                    {readiness.simulationSeed != null ? String(readiness.simulationSeed) : emDash}
                                </Descriptions.Item>
                            </Descriptions>
                            {readiness.summary?.trim() ? (
                                <Typography.Paragraph style={{ marginTop: 12, marginBottom: 0 }}>
                                    <Typography.Text strong>{t('finanzOnlineOutbox.readiness.apiSummary')}: </Typography.Text>
                                    <Typography.Text code style={{ whiteSpace: 'pre-wrap' }}>
                                        {readiness.summary.trim()}
                                    </Typography.Text>
                                </Typography.Paragraph>
                            ) : null}
                            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                                {t('finanzOnlineOutbox.readiness.healthProbeHint')}
                            </Typography.Paragraph>
                            <Divider orientation="left" plain>
                                {t('finanzOnlineOutbox.readiness.findingsTitle')}
                            </Divider>
                            <Typography.Paragraph type="secondary" style={{ marginTop: -8, marginBottom: 12, fontSize: 12 }}>
                                {t('finanzOnlineOutbox.readiness.findingsDetailHint')}
                            </Typography.Paragraph>
                            {(readiness.findings ?? []).length === 0 ? (
                                <Typography.Text type="secondary">—</Typography.Text>
                            ) : (
                                <Space orientation="vertical" size="small" style={{ width: '100%' }}>
                                    {(readiness.findings ?? []).map((f, i) => (
                                        <Alert
                                            key={`${f.code ?? 'finding'}-${i}`}
                                            type={readinessFindingAlertType(f.severity)}
                                            showIcon
                                            title={
                                                <Typography.Text strong style={{ display: 'block' }}>
                                                    {finanzOnlineReadinessFindingGermanTitle(t, f.code)}
                                                </Typography.Text>
                                            }
                                            description={
                                                <Space orientation="vertical" size={6} style={{ width: '100%' }}>
                                                    <Space wrap size="small">
                                                        {f.code ? <Tag>{f.code}</Tag> : null}
                                                        {f.severity ? <Tag>{f.severity}</Tag> : null}
                                                    </Space>
                                                    <Typography.Text type="secondary" style={{ whiteSpace: 'pre-wrap' }}>
                                                        {f.message ?? emDash}
                                                    </Typography.Text>
                                                </Space>
                                            }
                                        />
                                    ))}
                                </Space>
                            )}
                            <Divider orientation="left" plain>
                                {t('finanzOnlineOutbox.readiness.outboxCountsTitle')}
                            </Divider>
                            {outboxCountEntries.length === 0 ? (
                                <Typography.Text type="secondary">{emDash}</Typography.Text>
                            ) : (
                                <Descriptions bordered size="small" column={1}>
                                    {outboxCountEntries.map(([statusKey, n]) => (
                                        <Descriptions.Item key={statusKey} label={statusKey}>
                                            {n}
                                        </Descriptions.Item>
                                    ))}
                                </Descriptions>
                            )}
                        </>
                    ) : (
                        <Empty />
                    )}
                </Spin>
            </Card>

            <Card size="small" style={{ marginBottom: 16 }}>
                <Space wrap size="middle" align="start">
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.bucket')}
                        </Typography.Text>
                        <Select
                            style={{ width: 280 }}
                            value={bucket}
                            onChange={setBucket}
                            options={bucketOptions}
                            disabled={Boolean(statusCsv.trim())}
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.statusCsv')}
                        </Typography.Text>
                        <Input
                            style={{ width: 260 }}
                            placeholder={t('finanzOnlineOutbox.filters.statusCsvPlaceholder')}
                            value={statusCsv}
                            onChange={(e) => setStatusCsv(e.target.value)}
                            allowClear
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.mode')}
                        </Typography.Text>
                        <Select
                            style={{ width: 120 }}
                            allowClear
                            placeholder={t('finanzOnlineOutbox.filters.modePlaceholderAll')}
                            value={mode}
                            onChange={setMode}
                            options={[
                                { value: 'TEST', label: 'TEST' },
                                { value: 'PROD', label: 'PROD' },
                            ]}
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.correlationId')}
                        </Typography.Text>
                        <Input
                            style={{ width: 220 }}
                            value={correlationId}
                            onChange={(e) => setCorrelationId(e.target.value)}
                            allowClear
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.businessKey')}
                        </Typography.Text>
                        <Input
                            style={{ width: 200 }}
                            value={businessKey}
                            onChange={(e) => setBusinessKey(e.target.value)}
                            allowClear
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.createdDateRange')}
                        </Typography.Text>
                        <DatePicker.RangePicker format={DAYJS_DATE_FORMAT}
                            value={dateRange}
                            onChange={(r) => setDateRange(r ?? [null, null])}
                        />
                    </div>
                </Space>
            </Card>

            {error ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('finanzOnlineOutbox.error.listLoadTitle')}
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={error}
                            logContext="FinanzOnlineOutbox.list"
                            fallbackKey="finanzOnlineOutbox.error.listLoadTitle"
                        />
                    }
                />
            ) : null}

            <Spin spinning={isLoading || isFetching}>
                {items.length === 0 && !isLoading && !isFetching ? (
                    <Empty description={t('finanzOnlineOutbox.empty.noRows')} />
                ) : (
                    <Table<FinanzOnlineOutboxItemDto>
                        size="small"
                        rowKey={(r) => r.outboxId ?? `${r.businessKey}-${r.createdAtUtc}`}
                        columns={columns}
                        dataSource={items}
                        loading={isLoading || isFetching}
                        scroll={{ x: 2000 }}
                        pagination={{
                            pageSize: 50,
                            showSizeChanger: true,
                            showTotal: () => t('finanzOnlineOutbox.pagination.showTotal', { total, shown: items.length }),
                        }}
                        onRow={(record) => ({
                            onClick: () => openDrawer(record),
                            style: { cursor: 'pointer' },
                        })}
                    />
                )}
            </Spin>

            <Drawer
                title={t('finanzOnlineOutbox.drawer.title')}
                size={640}
                open={drawerOpen}
                onClose={closeDrawer}
                destroyOnHidden
            >
                {detailLoading && !displayRow ? (
                    <Spin />
                ) : displayRow ? (
                    <>
                    <Descriptions bordered size="small" column={1}>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.outboxId')}>
                            {displayRow.outboxId}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.businessKey')}>
                            {displayRow.businessKey ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.correlation')}>
                            {displayRow.correlationId ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.mode')}>
                            {modeTag(displayRow.mode)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.transportPath')}>
                            {transportPathTag(displayRow.transportPathKind)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.statusTechnical')}>
                            {displayRow.status ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.statusDisplay')}>
                            <Tag color={finanzOnlineOutboxStatusTagColor(displayRow.status, displayRow.transportPathKind)}>
                                {displayRow.operatorStatusLabel ?? emDash}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.hint')}>
                            {displayRow.operatorFailureHint ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.lastErrorCode')}>
                            {displayRow.lastErrorCode ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.lastErrorSummary')}>
                            {displayRow.lastErrorSummary ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.failureCategory')}>
                            {displayRow.failureCategory ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.attempts')}>
                            {displayRow.attemptCount ?? 0}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.nextAttemptUtc')}>
                            {fmtUtc(displayRow.nextAttemptAtUtc)}
                        </Descriptions.Item>
                    </Descriptions>
                    <Divider orientation="left" plain style={{ marginTop: 16 }}>
                        {t('finanzOnlineOutbox.drawer.evidenceSectionTitle')}
                    </Divider>
                    {displayRow.status === 'ProtocolSuccess' &&
                    isSimulatedFinanzOnlineTransportPath(displayRow.transportPathKind) ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 12 }}
                            title={t('finanzOnlineOutbox.drawer.protocolSuccessSimulatedAlert')}
                        />
                    ) : null}
                    <Descriptions bordered size="small" column={1}>
                        <Descriptions.Item
                            label={
                                <Typography.Text strong>
                                    {t('finanzOnlineOutbox.drawer.fields.evidenceTransmissionTitle')}
                                </Typography.Text>
                            }
                        >
                            {displayRow.transmissionId?.trim() ? (
                                <Typography.Text code copyable={{ text: displayRow.transmissionId }}>
                                    {displayRow.transmissionId}
                                </Typography.Text>
                            ) : (
                                emDash
                            )}
                        </Descriptions.Item>
                        <Descriptions.Item
                            label={
                                <Typography.Text strong>
                                    {t('finanzOnlineOutbox.drawer.fields.evidenceReferenceTitle')}
                                </Typography.Text>
                            }
                        >
                            <Space orientation="vertical" size={0}>
                                <span>{displayRow.externalReferenceId ?? emDash}</span>
                                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                    {t('finanzOnlineOutbox.drawer.fields.externalStatus')}:{' '}
                                    {displayRow.externalStatus ?? emDash}
                                </Typography.Text>
                            </Space>
                        </Descriptions.Item>
                        <Descriptions.Item
                            label={
                                <Typography.Text strong>
                                    {t('finanzOnlineOutbox.drawer.fields.evidenceProtocolTitle')}
                                </Typography.Text>
                            }
                        >
                            <Space orientation="vertical" size={4} style={{ width: '100%' }}>
                                <div>
                                    <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                        {t('finanzOnlineOutbox.drawer.fields.protocolCode')}
                                    </Typography.Text>
                                    <Typography.Text code style={{ whiteSpace: 'pre-wrap' }}>
                                        {displayRow.protocolCode?.trim() || emDash}
                                    </Typography.Text>
                                </div>
                                <div>
                                    <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                        {t('finanzOnlineOutbox.drawer.fields.protocolShort')}
                                    </Typography.Text>
                                    <Typography.Text style={{ whiteSpace: 'pre-wrap' }}>
                                        {displayRow.protocolSummary?.trim() || emDash}
                                    </Typography.Text>
                                </div>
                            </Space>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.protocolPayloadHash')}>
                            {displayRow.protocolPayloadHash ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.createdUtc')}>
                            {fmtUtc(displayRow.createdAtUtc)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.processedUtc')}>
                            {fmtUtc(displayRow.processedAtUtc)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.terminal')}>
                            {displayRow.isTerminal ? t('common.buttons.yes') : t('common.buttons.no')}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.idempotencySuffix')}>
                            {displayRow.idempotencyKeySuffix ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.scope')}>
                            {displayRow.scope
                                ? `${displayRow.scope.tenantId ?? emDash} / ${displayRow.scope.branchId ?? emDash} / ${displayRow.scope.registerId ?? emDash}`
                                : `${displayRow.tenantId ?? emDash} / ${displayRow.branchId ?? emDash} / ${displayRow.registerId ?? emDash}`}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.responsePreview')}>
                            <Typography.Paragraph style={{ marginBottom: 0, fontSize: 12, whiteSpace: 'pre-wrap' }}>
                                {displayRow.lastResponsePreview ?? emDash}
                            </Typography.Paragraph>
                        </Descriptions.Item>
                    </Descriptions>
                    </>
                ) : (
                    <Empty />
                )}
            </Drawer>
        </>
    );
}
