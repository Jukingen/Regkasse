'use client';

/**
 * License overview for the admin dashboard: KPIs, activation chart, recent activity, shortcuts.
 */

import React, { useMemo, useState } from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import {
    Alert,
    Button,
    Card,
    Col,
    Row,
    Segmented,
    Space,
    Spin,
    Statistic,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, KeyOutlined, PlusOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import utc from 'dayjs/plugin/utc';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDate } from '@/i18n';
import {
    downloadLicenseDashboardReportCsv,
    getLicenseDashboardActivationSeries,
    getLicenseDashboardRecentActivity,
    getLicenseDashboardSummary,
    licenseDashboardQueryKeys,
    type LicenseDashboardActivityRowDto,
} from '@/api/manual/adminLicense';

dayjs.extend(utc);
dayjs.extend(isoWeek);

const LicenseDashboardBarChart = dynamic(
    () => import('./LicenseDashboardBarChart'),
    {
        ssr: false,
        loading: () => (
            <div style={{ height: 280, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Spin />
            </div>
        ),
    },
);

function formatPeriodLabel(iso: string, granularity: 'day' | 'week', formatLocale: string): string {
    const d = dayjs.utc(iso);
    if (!d.isValid()) return iso;
    if (granularity === 'day') {
        return formatDate(iso, formatLocale, { month: '2-digit', day: '2-digit' });
    }
    return `${d.format('DD.MM.')} (KW ${d.isoWeek()})`;
}

function actionTagColor(action: string): string {
    switch (action) {
        case 'activate':
            return 'green';
        case 'extend':
            return 'blue';
        case 'revoke':
            return 'red';
        case 'cancel':
            return 'magenta';
        case 'delete':
            return 'volcano';
        case 'unregister':
            return 'orange';
        case 'details':
            return 'default';
        default:
            return 'default';
    }
}

function actionLabelKey(action: string): string {
    const map: Record<string, string> = {
        activate: 'license.dashboard.actionActivate',
        extend: 'license.dashboard.actionExtend',
        revoke: 'license.dashboard.actionRevoke',
        cancel: 'license.dashboard.actionCancel',
        delete: 'license.dashboard.actionDelete',
        unregister: 'license.dashboard.actionUnregister',
        details: 'license.dashboard.actionDetails',
        other: 'license.dashboard.actionOther',
    };
    return map[action] ?? 'license.dashboard.actionOther';
}

export function LicenseDashboardSection() {
    const { t, formatLocale } = useI18n();
    const [granularity, setGranularity] = useState<'day' | 'week'>('day');
    const [exportingCsv, setExportingCsv] = useState(false);
    const lookbackDays = 30;

    const summaryQuery = useQuery({
        queryKey: licenseDashboardQueryKeys.summary,
        queryFn: () => getLicenseDashboardSummary(),
    });

    const seriesQuery = useQuery({
        queryKey: licenseDashboardQueryKeys.series(granularity, lookbackDays),
        queryFn: () =>
            getLicenseDashboardActivationSeries({
                granularity,
                lookbackDays,
            }),
    });

    const activityQuery = useQuery({
        queryKey: licenseDashboardQueryKeys.activity(25),
        queryFn: () => getLicenseDashboardRecentActivity(25),
    });

    const chartData = useMemo(() => {
        const pts = seriesQuery.data?.points ?? [];
        return pts.map((p) => ({
            name: formatPeriodLabel(p.periodStartUtc, granularity, formatLocale),
            count: p.count,
        }));
    }, [seriesQuery.data?.points, granularity, formatLocale]);

    const activityColumns: ColumnsType<LicenseDashboardActivityRowDto> = useMemo(
        () => [
            {
                title: t('license.dashboard.colTime'),
                dataIndex: 'timestampUtc',
                key: 'timestampUtc',
                width: 170,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                        second: '2-digit',
                    }),
            },
            {
                title: t('license.dashboard.colKey'),
                dataIndex: 'licenseKeyMasked',
                key: 'licenseKeyMasked',
                ellipsis: true,
                render: (k: string) => (
                    <Typography.Text code style={{ fontSize: 12 }}>
                        {k}
                    </Typography.Text>
                ),
            },
            {
                title: t('license.dashboard.colMachine'),
                dataIndex: 'machineFingerprintShort',
                key: 'machineFingerprintShort',
                width: 160,
                ellipsis: true,
                render: (v: string | null | undefined) =>
                    v ? (
                        <Typography.Text code style={{ fontSize: 12 }}>
                            {v}
                        </Typography.Text>
                    ) : (
                        '—'
                    ),
            },
            {
                title: t('license.dashboard.colAction'),
                dataIndex: 'action',
                key: 'action',
                width: 160,
                render: (action: string) => <Tag color={actionTagColor(action)}>{t(actionLabelKey(action))}</Tag>,
            },
        ],
        [formatLocale, t],
    );

    const loadError =
        summaryQuery.isError || seriesQuery.isError || activityQuery.isError ? t('license.dashboard.loadFailed') : null;

    const exportCsv = async () => {
        setExportingCsv(true);
        try {
            await downloadLicenseDashboardReportCsv();
            message.success(t('license.dashboard.exportSuccess'));
        } catch {
            message.error(t('license.dashboard.exportFailed'));
        } finally {
            setExportingCsv(false);
        }
    };

    return (
        <Card title={t('license.dashboard.sectionTitle')} style={{ marginBottom: 24 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                {t('license.dashboard.sectionSubtitle')}
            </Typography.Paragraph>

            {loadError ? (
                <Alert type="warning" showIcon message={loadError} style={{ marginBottom: 16 }} />
            ) : null}

            <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small" loading={summaryQuery.isLoading}>
                        <Statistic title={t('license.dashboard.statActive')} value={summaryQuery.data?.activeLicenses ?? 0} />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small" loading={summaryQuery.isLoading}>
                        <Statistic
                            title={t('license.dashboard.statExpiring30')}
                            value={summaryQuery.data?.expiringWithin30Days ?? 0}
                            valueStyle={{ color: '#d48806' }}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small" loading={summaryQuery.isLoading}>
                        <Statistic
                            title={t('license.dashboard.statExpired')}
                            value={summaryQuery.data?.expiredLicenses ?? 0}
                            valueStyle={{ color: '#cf1322' }}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small" loading={summaryQuery.isLoading}>
                        <Statistic title={t('license.dashboard.statDevices')} value={summaryQuery.data?.uniqueActivatedDevices ?? 0} />
                    </Card>
                </Col>
            </Row>

            <Row gutter={[16, 16]}>
                <Col xs={24} lg={15}>
                    <Card
                        title={t('license.dashboard.chartTitle')}
                        extra={
                            <Segmented
                                size="small"
                                value={granularity}
                                onChange={(v) => setGranularity(v as 'day' | 'week')}
                                options={[
                                    { label: t('license.dashboard.chartDay'), value: 'day' },
                                    { label: t('license.dashboard.chartWeek'), value: 'week' },
                                ]}
                            />
                        }
                    >
                        {seriesQuery.isLoading ? (
                            <div style={{ height: 280, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <Spin />
                            </div>
                        ) : chartData.length === 0 ? (
                            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                                {t('license.dashboard.chartEmpty')}
                            </Typography.Paragraph>
                        ) : (
                            <LicenseDashboardBarChart data={chartData} />
                        )}
                    </Card>
                </Col>
                <Col xs={24} lg={9}>
                    <Card title={t('license.dashboard.quickTitle')}>
                        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                            <Link href="/admin/license">
                                <Button type="primary" block icon={<PlusOutlined />}>
                                    {t('license.dashboard.quickNewLicense')}
                                </Button>
                            </Link>
                            <Link href="/admin/license">
                                <Button block icon={<KeyOutlined />}>
                                    {t('license.dashboard.quickManage')}
                                </Button>
                            </Link>
                            <Button block icon={<DownloadOutlined />} loading={exportingCsv} onClick={() => void exportCsv()}>
                                {t('license.dashboard.quickExportCsv')}
                            </Button>
                        </Space>
                    </Card>
                </Col>
            </Row>

            <div style={{ height: 16 }} />

            <Card title={t('license.dashboard.activityTitle')}>
                <Table<LicenseDashboardActivityRowDto>
                    size="small"
                    rowKey={(row, i) => `lic-act-${row.timestampUtc}-${row.sourceCode}-${i}`}
                    loading={activityQuery.isLoading}
                    pagination={false}
                    dataSource={activityQuery.data?.items ?? []}
                    columns={activityColumns}
                    locale={{ emptyText: '—' }}
                    scroll={{ x: 720 }}
                />
            </Card>
        </Card>
    );
}
