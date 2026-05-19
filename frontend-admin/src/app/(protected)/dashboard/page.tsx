'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { Card, Col, Row, Statistic, DatePicker, Table, Spin, Typography } from 'antd';
import { DollarOutlined, ShoppingOutlined, UserOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { MonatsbelegComplianceTable } from '@/features/dashboard/components/MonatsbelegComplianceTable';
import { OfflineQueueDashboardCard } from '@/features/dashboard/components/OfflineQueueDashboardCard';
import { useAdminMonatsbelegOverview } from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import {
    useGetApiReportsSales,
    useGetApiReportsProducts,
    useGetApiReportsPayments,
    useGetApiReportsCustomers
} from '@/api/generated/reports/reports';
import { HospitalityQuickLinksCard } from '@/features/dashboard/components/HospitalityQuickLinksCard';
import { TimeSyncDriftAlertCard } from '@/features/dashboard/components/TimeSyncDriftAlertCard';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';
import { RksvReminderStatusCard } from '@/features/dashboard/components/RksvReminderStatusCard';
import { LicenseDashboardSection } from '@/features/dashboard/components/LicenseDashboardSection';
import dayjs from 'dayjs';

const { RangePicker } = DatePicker;

export default function DashboardPage() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const offlineQueueCardEnabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);
    const monatsbelegOverviewEnabled = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);
    const tseHealthCardEnabled = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);
    const timeSyncDriftAlertEnabled = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
    const licenseDashboardEnabled = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
    const monatsbelegOverview = useAdminMonatsbelegOverview(monatsbelegOverviewEnabled);

    const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
        dayjs().startOf('month'),
        dayjs().endOf('month')
    ]);

    const startDate = dateRange[0].format('YYYY-MM-DD');
    const endDate = dateRange[1].format('YYYY-MM-DD');

    // 1. Sales Report
    const { data: salesReport, isLoading: loadingSales } = useGetApiReportsSales({
        startDate,
        endDate
    });

    // 2. Products Report
    const { data: productsReport } = useGetApiReportsProducts({
        startDate,
        endDate,
    });

    // 3. Payment Methods
    const { data: paymentsReport } = useGetApiReportsPayments({
        startDate,
        endDate
    });

    // 4. Customer Stats
    const { data: customersReport } = useGetApiReportsCustomers({
        startDate,
        endDate,
    });

    const loading = loadingSales;

    return (
        <div style={{ paddingBottom: 24 }}>
            <AdminPageHeader
                title={t('nav.overview')}
                breadcrumbs={[adminOverviewCrumb(t)]}
                actions={
                    <RangePicker
                        value={dateRange}
                        onChange={(dates) => {
                            if (dates && dates[0] && dates[1]) {
                                setDateRange([dates[0], dates[1]]);
                            }
                        }}
                    />
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Geschäftszahlen für den gewählten Zeitraum (Umsatz, Verteilung, Top-Artikel). Operative Kassenberichte:{' '}
                    <Link href="/reporting">{t('nav.reporting')}</Link>. RKSV: Seitenleiste unter «RKSV».
                </Typography.Paragraph>
            </AdminPageHeader>

            {licenseDashboardEnabled ? <LicenseDashboardSection /> : null}

            {offlineQueueCardEnabled ? <OfflineQueueDashboardCard /> : null}

            {timeSyncDriftAlertEnabled ? <TimeSyncDriftAlertCard /> : null}

            {tseHealthCardEnabled ? <TseHealthCard /> : null}

            {monatsbelegOverviewEnabled ? <RksvReminderStatusCard enabled={monatsbelegOverviewEnabled} /> : null}

            {monatsbelegOverviewEnabled ? (
                <MonatsbelegComplianceTable
                    rows={monatsbelegOverview.rows}
                    loading={monatsbelegOverview.registersLoading || monatsbelegOverview.statusPending}
                />
            ) : null}

            {loading ? (
                <div
                    role="status"
                    aria-live="polite"
                    aria-busy="true"
                    aria-label="Berichte werden geladen"
                    style={{ textAlign: 'center', padding: 50 }}
                >
                    <Spin size="large" tip="Berichte werden geladen…" />
                </div>
            ) : (
                <>
            <HospitalityQuickLinksCard />
            {/* Key Metrics Row */}
            <Row gutter={16} style={{ marginBottom: 24 }}>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Gesamtumsatz"
                            value={salesReport?.totalSales}
                            precision={2}
                            valueStyle={{ color: '#3f8600' }}
                            prefix={<DollarOutlined />}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Anzahl Verkäufe"
                            value={salesReport?.totalInvoices}
                            prefix={<ShoppingOutlined />}
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Ø Verkauf"
                            value={salesReport?.averageOrderValue}
                            precision={2}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Aktive Kunden"
                            value={customersReport?.totalCustomers}
                            prefix={<UserOutlined />}
                        />
                    </Card>
                </Col>
            </Row>

            <Row gutter={16}>
                {/* Top Selling Products */}
                <Col span={12}>
                    <Card title="Meistverkaufte Produkte" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={(productsReport?.topSellingProducts ?? []).slice(0, 5)}
                            pagination={false}
                            rowKey="productId"
                            columns={[
                                { title: 'Produkt', dataIndex: 'productName' },
                                { title: 'Menge', dataIndex: 'quantitySold' },
                                { title: 'Umsatz', dataIndex: 'revenue', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>

                {/* Sales by Payment Method */}
                <Col span={12}>
                    <Card title="Zahlungsarten" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={paymentsReport?.paymentsByMethod || []}
                            pagination={false}
                            rowKey="method"
                            columns={[
                                { title: 'Zahlungsart', dataIndex: 'method' },
                                { title: 'Anzahl', dataIndex: 'count' },
                                { title: 'Summe', dataIndex: 'total', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>
            </Row>

            <div style={{ height: 24 }} />

            <Row gutter={16}>
                {/* Top Customers */}
                <Col span={12}>
                    <Card title="Top-Kunden" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={(customersReport?.topCustomers ?? []).slice(0, 5)}
                            pagination={false}
                            rowKey="customerId"
                            columns={[
                                { title: 'Kunde', dataIndex: 'customerName' },
                                { title: 'Bestellungen', dataIndex: 'orderCount' },
                                { title: 'Ausgaben', dataIndex: 'totalSpent', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>
            </Row>
                </>
            )}
        </div>
    );
}
