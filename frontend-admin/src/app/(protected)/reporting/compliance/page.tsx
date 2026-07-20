'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Compliance- und Betriebsberichte: Mutabakat, TSE-Kette, Offline-Wiederherstellung, Peak-Hours, Warenbewegung.
 */
import React, { useMemo, useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Row, Select, Space, Statistic, Table, Dropdown, Tabs, Tag, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { CalendarOutlined, DownloadOutlined, SettingOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { CardSkeleton, TableSkeleton } from '@/components/Skeleton';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { usePermissions } from '@/shared/auth/usePermissions';
import type { CashRegister } from '@/api/generated/model';
import {
    downloadTseChainExport,
    fetchDailyReconciliation,
    fetchOfflineRecovery,
    fetchPeakHours,
    fetchProductMovement,
    fetchTseChainContinuity,
    downloadAdminReportExport,
    fetchUserPerformance,
    type AdminReportType,
    type TseContinuityRegisterReport,
    type UserPerformanceRow,
} from '@/features/reporting/compliance/complianceReportsApi';
import { HeatmapChart } from '@/features/reports/components/HeatmapChart';
import { ReportConfigModal } from '@/features/reports/components/ReportConfigModal';
import { ReportSelector, type ReportCategoryId } from '@/features/reports/components/ReportSelector';
import { ScheduleReportModal } from '@/features/reports/components/ScheduleReportModal';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const { RangePicker } = DatePicker;

const ADMIN_REPORT_BY_TAB: Record<ReportCategoryId, AdminReportType> = {
    reconciliation: 'DailyReconciliation',
    tse: 'TseContinuity',
    offline: 'OfflineRecovery',
    users: 'UserPerformance',
    peak: 'PeakHours',
    movement: 'ProductMovement',
};

function normalizeRegisters(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    const r = (data as { registers?: CashRegister[] } | undefined)?.registers;
    return Array.isArray(r) ? r : [];
}

export default function ComplianceReportingPage() {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const { hasPermission } = usePermissions();
    const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);
    const [tab, setTab] = useState<ReportCategoryId>('reconciliation');
    const [configOpen, setConfigOpen] = useState(false);
    const [scheduleOpen, setScheduleOpen] = useState(false);
    const [businessDay, setBusinessDay] = useState(() => dayjs());
    const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
        dayjs().startOf('month'),
        dayjs().endOf('month'),
    ]);
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

    const startDate = dateRange[0].format('YYYY-MM-DD');
    const endDate = dateRange[1].format('YYYY-MM-DD');
    const businessDateParam = businessDay.format('YYYY-MM-DD');

    const registersQ = useGetApiCashRegister();
    const registerRows = useMemo(() => normalizeRegisters(registersQ.data as unknown), [registersQ.data]);
    const registerOptions = useMemo(
        () =>
            registerRows
                .filter((r) => r.id)
                .map((r) => ({
                    value: r.id as string,
                    label: `${r.registerNumber} — ${r.location ?? ''}`,
                })),
        [registerRows],
    );

    const reconciliationQ = useQuery({
        queryKey: ['compliance', 'reconciliation', businessDateParam, cashRegisterId],
        queryFn: () =>
            fetchDailyReconciliation({ businessDate: businessDateParam, cashRegisterId }),
        enabled: tab === 'reconciliation',
    });

    const tseQ = useQuery({
        queryKey: ['compliance', 'tse', startDate, endDate, cashRegisterId],
        queryFn: () => fetchTseChainContinuity({ startDate, endDate, cashRegisterId }),
        enabled: tab === 'tse',
    });

    const offlineQ = useQuery({
        queryKey: ['compliance', 'offline', startDate, endDate, cashRegisterId],
        queryFn: () => fetchOfflineRecovery({ startDate, endDate, cashRegisterId }),
        enabled: tab === 'offline',
    });

    const peakQ = useQuery({
        queryKey: ['compliance', 'peak', startDate, endDate, cashRegisterId],
        queryFn: () => fetchPeakHours({ startDate, endDate, cashRegisterId }),
        enabled: tab === 'peak',
    });

    const movementQ = useQuery({
        queryKey: ['compliance', 'movement', startDate, endDate],
        queryFn: () => fetchProductMovement({ startDate, endDate }),
        enabled: tab === 'movement',
    });

    const userPerfQ = useQuery({
        queryKey: ['compliance', 'userPerf', startDate, endDate, cashRegisterId],
        queryFn: () => fetchUserPerformance({ startDate, endDate, cashRegisterId }),
        enabled: tab === 'users',
    });

    const money = (v: number) => formatCurrency(v, formatLocale);

    const onDownloadTseChain = async (row: TseContinuityRegisterReport, format: 'csv' | 'json') => {
        if (!canExport) {
            message.warning(t('adminShell.reporting.exportNoPermission'));
            return;
        }
        try {
            const blob = await downloadTseChainExport(
                format === 'csv' ? row.detailsExportPath : row.detailsExportJsonPath,
                format,
            );
            const w = globalThis as unknown as {
                URL: { createObjectURL: (b: unknown) => string; revokeObjectURL: (u: string) => void };
                document: { createElement: (t: string) => { href: string; download: string; click: () => void } };
            };
            const url = w.URL.createObjectURL(blob);
            const a = w.document.createElement('a');
            a.href = url;
            a.download = `tse-chain-${row.registerNumber ?? row.cashRegisterId}.${format}`;
            a.click();
            w.URL.revokeObjectURL(url);
        } catch {
            message.error(t('reporting.compliance.tseExportFailed'));
        }
    };

    const tseRegisterCols: ColumnsType<TseContinuityRegisterReport> = [
        { title: t('reporting.compliance.columns.register'), dataIndex: 'registerNumber', width: 120 },
        { title: t('reporting.compliance.columns.signatures'), dataIndex: 'signatureCount', width: 90 },
        { title: t('reporting.compliance.columns.gaps'), dataIndex: 'gapsCount', width: 70 },
        { title: t('reporting.compliance.columns.duplicates'), dataIndex: 'duplicateCount', width: 90 },
        {
            title: t('reporting.compliance.columns.firstSig'),
            dataIndex: 'firstSignatureAtUtc',
            render: (v: string | undefined) =>
                v ? formatDateTime(v, formatLocale, { dateStyle: 'short', timeStyle: 'medium' }) : '—',
        },
        {
            title: t('reporting.compliance.columns.lastSig'),
            dataIndex: 'lastSignatureAtUtc',
            render: (v: string | undefined) =>
                v ? formatDateTime(v, formatLocale, { dateStyle: 'short', timeStyle: 'medium' }) : '—',
        },
        {
            title: t('reporting.compliance.columns.status'),
            key: 'status',
            width: 140,
            render: (_: unknown, row) => (
                <Space size={4}>
                    {row.hasGaps ? <Tag color="warning">{t('reporting.compliance.tags.gaps')}</Tag> : null}
                    {row.hasDuplicates ? <Tag color="error">{t('reporting.compliance.tags.duplicates')}</Tag> : null}
                    {!row.hasGaps && !row.hasDuplicates ? <Tag color="success">OK</Tag> : null}
                </Space>
            ),
        },
        {
            title: t('reporting.compliance.columns.maxGapSec'),
            dataIndex: 'maxGapDurationSeconds',
            width: 110,
            render: (v: number) => Math.round(Number(v ?? 0)),
        },
        {
            title: t('reporting.compliance.columns.export'),
            key: 'export',
            width: 160,
            render: (_: unknown, row) =>
                canExport ? (
                    <Space>
                        <Button
                            size="small"
                            icon={<DownloadOutlined />}
                            onClick={() => void onDownloadTseChain(row, 'csv')}
                        >
                            CSV
                        </Button>
                        <Button size="small" onClick={() => void onDownloadTseChain(row, 'json')}>
                            JSON
                        </Button>
                    </Space>
                ) : (
                    <Typography.Text type="secondary">—</Typography.Text>
                ),
        },
    ];

    const userPerfCols: ColumnsType<UserPerformanceRow> = [
        {
            title: t('reporting.compliance.columns.user'),
            key: 'user',
            render: (_: unknown, row) => (
                <Space orientation="vertical" size={0}>
                    <Typography.Text strong>{row.userName}</Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {row.role || '—'} · {row.userId}
                    </Typography.Text>
                </Space>
            ),
        },
        { title: t('reporting.compliance.columns.txCount'), dataIndex: 'transactionCount', width: 70 },
        {
            title: t('reporting.compliance.columns.revenue'),
            dataIndex: 'totalAmount',
            align: 'right',
            render: (v: number) => money(Number(v ?? 0)),
        },
        {
            title: t('reporting.compliance.columns.stornoRate'),
            dataIndex: 'stornoRate',
            width: 90,
            render: (v: number, row) => (
                <Typography.Text type={row.stornoRate > 0.05 ? 'danger' : undefined}>
                    {(Number(v ?? 0) * 100).toFixed(1)}%
                </Typography.Text>
            ),
        },
        {
            title: t('reporting.compliance.columns.txPerHour'),
            dataIndex: 'transactionsPerHour',
            width: 90,
            render: (v: number) => Number(v ?? 0).toFixed(1),
        },
        {
            title: t('reporting.compliance.columns.avgProcSec'),
            dataIndex: 'averageProcessingSeconds',
            width: 100,
            render: (v: number) => Math.round(Number(v ?? 0)),
        },
        {
            title: t('reporting.compliance.columns.activeHours'),
            dataIndex: 'activeHours',
            width: 90,
            render: (v: number) => Number(v ?? 0).toFixed(2),
        },
    ];

    const movementCols: ColumnsType<{ productId: string; productName: string; quantitySold: number; revenue: number }> =
        [
            { title: t('reporting.compliance.columns.product'), dataIndex: 'productName' },
            { title: t('reporting.compliance.columns.qty'), dataIndex: 'quantitySold', width: 90 },
            {
                title: t('reporting.compliance.columns.revenue'),
                dataIndex: 'revenue',
                align: 'right',
                render: (v: number) => money(Number(v ?? 0)),
            },
        ];

    const onExportReport = async (format: 'csv' | 'pdf' | 'json' | 'excel') => {
        if (!canExport) {
            message.warning(t('adminShell.reporting.exportNoPermission'));
            return;
        }
        try {
            const blob = await downloadAdminReportExport(ADMIN_REPORT_BY_TAB[tab], format, {
                startDate,
                endDate,
                businessDate: businessDateParam,
                cashRegisterId,
            });
            const ext = format === 'excel' ? 'csv' : format;
            const w = globalThis as unknown as {
                URL: { createObjectURL: (b: unknown) => string; revokeObjectURL: (u: string) => void };
                document: { createElement: (t: string) => { href: string; download: string; click: () => void } };
            };
            const url = w.URL.createObjectURL(blob);
            const a = w.document.createElement('a');
            a.href = url;
            a.download = `${tab}-report.${ext}`;
            a.click();
            w.URL.revokeObjectURL(url);
        } catch {
            message.error(t('reporting.compliance.exportFailed'));
        }
    };

    const exportMenu: MenuProps['items'] = [
        { key: 'csv', label: 'CSV', onClick: () => void onExportReport('csv') },
        { key: 'excel', label: 'Excel', onClick: () => void onExportReport('excel') },
        { key: 'pdf', label: 'PDF', onClick: () => void onExportReport('pdf') },
    ];

    const filterCard = (
        <Card size="small" style={{ marginBottom: 16 }}>
            <Row gutter={[16, 12]} align="bottom">
                {tab === 'reconciliation' ? (
                    <Col xs={24} md={8}>
                        <Typography.Text type="secondary">{t('reporting.compliance.businessDate')}</Typography.Text>
                        <DatePicker
                            style={{ width: '100%', marginTop: 4 }}
                            value={businessDay}
                            onChange={(d) => d && setBusinessDay(d)}
                            format="DD.MM.YYYY"
                        />
                    </Col>
                ) : (
                    <Col xs={24} md={12}>
                        <Typography.Text type="secondary">{t('reporting.compliance.dateRange')}</Typography.Text>
                        <RangePicker format={DAYJS_DATE_FORMAT}
                            style={{ width: '100%', marginTop: 4 }}
                            value={dateRange}
                            onChange={(d) => {
                                if (d?.[0] && d[1]) setDateRange([d[0], d[1]]);
                            }}
                        />
                    </Col>
                )}
                <Col xs={24} md={8}>
                    <Typography.Text type="secondary">{t('reporting.compliance.register')}</Typography.Text>
                    <Select
                        allowClear
                        placeholder={t('reporting.compliance.allRegisters')}
                        style={{ width: '100%', marginTop: 4 }}
                        options={registerOptions}
                        value={cashRegisterId}
                        onChange={setCashRegisterId}
                        loading={registersQ.isLoading}
                    />
                </Col>
                <Col xs={24} md={8}>
                    <Space wrap>
                        <Button icon={<SettingOutlined />} onClick={() => setConfigOpen(true)}>
                            {t('reporting.compliance.config.open')}
                        </Button>
                        {canExport ? (
                            <Dropdown menu={{ items: exportMenu }}>
                                <Button icon={<DownloadOutlined />}>{t('reporting.compliance.export')}</Button>
                            </Dropdown>
                        ) : null}
                        {canExport ? (
                            <Button icon={<CalendarOutlined />} onClick={() => setScheduleOpen(true)}>
                                {t('reporting.compliance.schedule.open')}
                            </Button>
                        ) : null}
                    </Space>
                </Col>
            </Row>
        </Card>
    );

    const rec = reconciliationQ.data;

    return (
        <div style={{ paddingBottom: 24 }}>
            <AdminPageHeader
                title={t('reporting.compliance.pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('nav.reporting'), href: '/reporting' },
                    { title: t('reporting.compliance.breadcrumb'), href: '/reporting/compliance' },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('reporting.compliance.intro')}{' '}
                    <Link href="/reporting/staff">{t('nav.staffPerformance')}</Link>
                    {' · '}
                    <Link href="/reports/daily-closing">{t('nav.dailyClosingSummary')}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            <ReportConfigModal
                open={configOpen}
                reportKey={tab}
                initial={{ businessDay, dateRange, cashRegisterId }}
                registerOptions={registerOptions}
                onCancel={() => setConfigOpen(false)}
                onApply={(v) => {
                    setBusinessDay(v.businessDay);
                    setDateRange(v.dateRange);
                    setCashRegisterId(v.cashRegisterId);
                    setConfigOpen(false);
                }}
            />
            <ScheduleReportModal
                open={scheduleOpen}
                reportType={ADMIN_REPORT_BY_TAB[tab]}
                filters={{
                    startDate,
                    endDate,
                    businessDate: businessDateParam,
                    cashRegisterId,
                }}
                onClose={() => setScheduleOpen(false)}
                onScheduled={() => message.success(t('reporting.compliance.schedule.saved'))}
            />

            <Row gutter={16}>
                <Col xs={24} md={5}>
                    <ReportSelector activeKey={tab} onChange={setTab} />
                </Col>
                <Col xs={24} md={19}>
                    {filterCard}
                    <Tabs tabBarStyle={{ display: 'none' }} activeKey={tab} onChange={(k) => setTab(k as ReportCategoryId)} items={[
                    {
                        key: 'reconciliation',
                        label: t('reporting.compliance.tabs.reconciliation'),
                        children: (
                            <Spin spinning={reconciliationQ.isLoading}>
                                {rec?.disclaimerDe ? (
                                    <Alert type="info" showIcon title={rec.disclaimerDe} style={{ marginBottom: 12 }} />
                                ) : null}
                                {rec?.notes ? (
                                    <Alert type="warning" showIcon title={rec.notes} style={{ marginBottom: 12 }} />
                                ) : null}
                                <Row gutter={16}>
                                    <Col xs={12} md={6}>
                                        <Statistic title={t('reporting.compliance.stats.cash')} value={money(rec?.cashTotal ?? 0)} />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic title={t('reporting.compliance.stats.card')} value={money(rec?.cardTotal ?? 0)} />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.expectedCash')}
                                            value={money(rec?.expectedCash ?? 0)}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.actualCash')}
                                            value={rec?.actualCash != null ? money(rec.actualCash) : '—'}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.difference')}
                                            value={
                                                rec?.cashDifference != null ? money(rec.cashDifference) : '—'
                                            }
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.reconciled')}
                                            value={rec?.isReconciled ? t('reporting.compliance.yes') : t('reporting.compliance.no')}
                                        />
                                    </Col>
                                </Row>
                            </Spin>
                        ),
                    },
                    {
                        key: 'tse',
                        label: t('reporting.compliance.tabs.tse'),
                        children: (
                            <Spin spinning={tseQ.isLoading}>
                                {tseQ.data?.operatorNoteDe ? (
                                    <Alert type="info" showIcon title={tseQ.data.operatorNoteDe} style={{ marginBottom: 12 }} />
                                ) : null}
                                <Row gutter={16} style={{ marginBottom: 16 }}>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.signatures')}
                                            value={tseQ.data?.totalSignatureCount ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.gaps')}
                                            value={tseQ.data?.totalGapsCount ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.duplicates')}
                                            value={tseQ.data?.totalDuplicateCount ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.stats.chainBreaks')}
                                            value={tseQ.data?.breakCount ?? 0}
                                        />
                                    </Col>
                                </Row>
                                <Table
                                    size="small"
                                    rowKey={(r) => r.cashRegisterId}
                                    dataSource={tseQ.data?.registers ?? []}
                                    pagination={false}
                                    scroll={{ x: 1100 }}
                                    columns={tseRegisterCols}
                                />
                            </Spin>
                        ),
                    },
                    {
                        key: 'offline',
                        label: t('reporting.compliance.tabs.offline'),
                        children: (
                            <Spin spinning={offlineQ.isLoading}>
                                {offlineQ.data?.operatorNoteDe ? (
                                    <Alert type="info" showIcon title={offlineQ.data.operatorNoteDe} style={{ marginBottom: 12 }} />
                                ) : null}
                                <Row gutter={16} style={{ marginBottom: 16 }}>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.offline.pendingStart')}
                                            value={offlineQ.data?.pendingAtStart ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.offline.pendingEnd')}
                                            value={offlineQ.data?.pendingAtEnd ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.offline.recoveredOk')}
                                            value={offlineQ.data?.recoveredSuccessfully ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.offline.recoveredRetry')}
                                            value={offlineQ.data?.recoveredWithRetry ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.offline.failedPermanent')}
                                            value={offlineQ.data?.permanentlyFailed ?? 0}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.offline.avgRecoverySec')}
                                            value={Math.round(offlineQ.data?.averageRecoverySeconds ?? 0)}
                                        />
                                    </Col>
                                </Row>
                                <Table
                                    size="small"
                                    style={{ marginBottom: 16 }}
                                    rowKey="cashRegisterId"
                                    pagination={false}
                                    dataSource={offlineQ.data?.byRegister ?? []}
                                    columns={[
                                        { title: t('reporting.compliance.columns.register'), dataIndex: 'registerNumber' },
                                        { title: 'Pending', dataIndex: 'pendingCount' },
                                        { title: 'Failed', dataIndex: 'failedCount' },
                                    ]}
                                />
                                <Typography.Link href="/admin/offline-transactions">
                                    {t('reporting.compliance.offlineAdminLink')}
                                </Typography.Link>
                                <Table
                                    style={{ marginTop: 16 }}
                                    size="small"
                                    rowKey="id"
                                    dataSource={offlineQ.data?.recentRows ?? []}
                                    pagination={{ pageSize: 10 }}
                                    columns={[
                                        {
                                            title: t('reporting.compliance.columns.time'),
                                            dataIndex: 'serverReceivedAtUtc',
                                            render: (v: string) =>
                                                formatDateTime(v, formatLocale, {
                                                    dateStyle: 'short',
                                                    timeStyle: 'medium',
                                                }),
                                        },
                                        { title: 'Status', dataIndex: 'status' },
                                        { title: t('reporting.compliance.columns.error'), dataIndex: 'lastError', ellipsis: true },
                                    ]}
                                />
                            </Spin>
                        ),
                    },
                    {
                        key: 'users',
                        label: t('reporting.compliance.tabs.users'),
                        children: (
                            <Spin spinning={userPerfQ.isLoading}>
                                <Typography.Paragraph type="secondary">
                                    <Link href="/reporting/staff">{t('nav.staffPerformance')}</Link>
                                </Typography.Paragraph>
                                {userPerfQ.data?.highStornoRateWarning?.length ? (
                                    <Alert
                                        type="warning"
                                        showIcon
                                        style={{ marginBottom: 12 }}
                                        title={t('reporting.compliance.users.stornoWarning', {
                                            count: userPerfQ.data.highStornoRateWarning.length,
                                            pct: (userPerfQ.data.highStornoRateThreshold * 100).toFixed(0),
                                        })}
                                    />
                                ) : null}
                                <Table
                                    size="small"
                                    rowKey="userId"
                                    dataSource={userPerfQ.data?.perUser ?? []}
                                    columns={userPerfCols}
                                    pagination={{ pageSize: 15 }}
                                    scroll={{ x: 900 }}
                                />
                            </Spin>
                        ),
                    },
                    {
                        key: 'peak',
                        label: t('reporting.compliance.tabs.peak'),
                        children: (
                            <Spin spinning={peakQ.isLoading}>
                                <Typography.Paragraph type="secondary">
                                    {t('reporting.compliance.peakHint')}
                                </Typography.Paragraph>
                                <HeatmapChart
                                    cells={peakQ.data?.cells}
                                    maxCellCount={peakQ.data?.maxCellCount}
                                    busiestLabel={
                                        peakQ.data?.busiestHour
                                            ? t('reporting.compliance.peak.busiest', {
                                                  day: peakQ.data.busiestHour.day,
                                                  hour: peakQ.data.busiestHour.hour,
                                                  count: peakQ.data.busiestHour.transactionCount,
                                              })
                                            : undefined
                                    }
                                    quietestLabel={
                                        peakQ.data?.quietestHour
                                            ? t('reporting.compliance.peak.quietest', {
                                                  day: peakQ.data.quietestHour.day,
                                                  hour: peakQ.data.quietestHour.hour,
                                                  count: peakQ.data.quietestHour.transactionCount,
                                              })
                                            : undefined
                                    }
                                />
                            </Spin>
                        ),
                    },
                    {
                        key: 'movement',
                        label: t('reporting.compliance.tabs.movement'),
                        children: (
                            <Spin spinning={movementQ.isLoading}>
                                <Row gutter={16} style={{ marginBottom: 16 }}>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.movement.turnover')}
                                            value={Number(movementQ.data?.stockTurnoverRate ?? 0).toFixed(2)}
                                        />
                                    </Col>
                                    <Col xs={12} md={6}>
                                        <Statistic
                                            title={t('reporting.compliance.movement.daysOnHand')}
                                            value={Number(movementQ.data?.daysOfInventoryOnHand ?? 0).toFixed(1)}
                                        />
                                    </Col>
                                </Row>
                                <Typography.Title level={5}>{t('reporting.compliance.movement.topRevenue')}</Typography.Title>
                                <Table
                                    size="small"
                                    rowKey="productId"
                                    columns={movementCols}
                                    dataSource={movementQ.data?.topSellingByRevenue ?? movementQ.data?.lines ?? []}
                                    pagination={{ pageSize: 15 }}
                                    style={{ marginBottom: 16 }}
                                />
                                <Typography.Title level={5}>{t('reporting.compliance.movement.slowMovers')}</Typography.Title>
                                <Table
                                    size="small"
                                    rowKey={(r) => `slow-${r.productId}`}
                                    columns={movementCols}
                                    dataSource={movementQ.data?.slowMovers ?? []}
                                    pagination={{ pageSize: 10 }}
                                />
                            </Spin>
                        ),
                    },
                ]}
                    />
                </Col>
            </Row>
        </div>
    );
}
