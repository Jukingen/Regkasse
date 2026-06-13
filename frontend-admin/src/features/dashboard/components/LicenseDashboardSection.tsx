'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * License overview for the admin dashboard: KPIs, activation chart, recent activity, shortcuts.
 */

import React, { useMemo, useState } from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import { Alert, Button, Card, Col, Row, Segmented, Space, Spin, Typography } from 'antd';
import { DownloadOutlined, KeyOutlined, PlusOutlined } from '@ant-design/icons';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import utc from 'dayjs/plugin/utc';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDate } from '@/i18n';
import {
    downloadLicenseDashboardReportCsv,
    getLicenseDashboardActivationSeries,
    licenseDashboardQueryKeys,
} from '@/api/manual/adminLicense';
import { LicenseStatsSection } from '@/features/dashboard/components/LicenseStatsSection';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';

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

export function LicenseDashboardSection() {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const { user } = useAuth();
    const isSuperAdminUser = isSuperAdmin(user?.role);
    const [granularity, setGranularity] = useState<'day' | 'week'>('day');
    const [exportingCsv, setExportingCsv] = useState(false);
    const lookbackDays = 30;

    const seriesQuery = useAuthorizedQuery({
        queryKey: licenseDashboardQueryKeys.series(granularity, lookbackDays),
        queryFn: () =>
            getLicenseDashboardActivationSeries({
                granularity,
                lookbackDays,
            }),
        requiredRole: 'SuperAdmin',
    });

    const chartData = useMemo(() => {
        const pts = seriesQuery.data?.points ?? [];
        return pts.map((p) => ({
            name: formatPeriodLabel(p.periodStartUtc, granularity, formatLocale),
            count: p.count,
        }));
    }, [seriesQuery.data?.points, granularity, formatLocale]);

    const chartLoadError = seriesQuery.isError ? t('license.dashboard.loadFailed') : null;

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

            <LicenseStatsSection />

            {isSuperAdminUser ? (
                <>
                    {chartLoadError ? (
                        <Alert type="warning" showIcon title={chartLoadError} style={{ marginTop: 16, marginBottom: 16 }} />
                    ) : null}

                    <Row gutter={[16, 16]} style={{ marginTop: 24 }}>
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
                                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
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
                </>
            ) : null}
        </Card>
    );
}
