'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { Card, Col, Row, Statistic, DatePicker, Table, Typography } from 'antd';
import { DollarOutlined, ShoppingOutlined, UserOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DashboardMonatsbelegSection } from '@/features/dashboard/components/DashboardMonatsbelegSection';
import { OfflineQueueDashboardCard } from '@/features/dashboard/components/OfflineQueueDashboardCard';
import { useDashboardBusinessReports } from '@/features/dashboard/hooks/useDashboardBusinessReports';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { HospitalityQuickLinksCard } from '@/features/dashboard/components/HospitalityQuickLinksCard';
import { TimeSyncDriftAlertCard } from '@/features/dashboard/components/TimeSyncDriftAlertCard';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';
import { LicenseDashboardSection } from '@/features/dashboard/components/LicenseDashboardSection';
import { RksvReminderCard } from '@/features/dashboard/components/RksvReminderCard';
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

    const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
        dayjs().startOf('month'),
        dayjs().endOf('month'),
    ]);

    const startDate = dateRange[0].format('YYYY-MM-DD');
    const endDate = dateRange[1].format('YYYY-MM-DD');

    const { sales, products, payments, customers } = useDashboardBusinessReports({ startDate, endDate });

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

            {monatsbelegOverviewEnabled ? <RksvReminderCard /> : null}

            {monatsbelegOverviewEnabled ? <DashboardMonatsbelegSection enabled={monatsbelegOverviewEnabled} /> : null}

            <HospitalityQuickLinksCard />

            <Row gutter={16} style={{ marginBottom: 24 }}>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} loading={sales.isLoading}>
                        <Statistic
                            title="Gesamtumsatz"
                            value={sales.data?.totalSales}
                            precision={2}
                            valueStyle={{ color: '#3f8600' }}
                            prefix={<DollarOutlined />}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} loading={sales.isLoading}>
                        <Statistic
                            title="Anzahl Verkäufe"
                            value={sales.data?.totalInvoices}
                            prefix={<ShoppingOutlined />}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} loading={sales.isLoading}>
                        <Statistic
                            title="Ø Verkauf"
                            value={sales.data?.averageOrderValue}
                            precision={2}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} loading={customers.isLoading}>
                        <Statistic
                            title="Aktive Kunden"
                            value={customers.data?.totalCustomers}
                            prefix={<UserOutlined />}
                        />
                    </Card>
                </Col>
            </Row>

            <Row gutter={16}>
                <Col span={12}>
                    <Card
                        title="Meistverkaufte Produkte"
                        bordered={false}
                        style={{ height: '100%' }}
                        loading={products.isLoading}
                    >
                        <Table
                            dataSource={(products.data?.topSellingProducts ?? []).slice(0, 5)}
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

                <Col span={12}>
                    <Card
                        title="Zahlungsarten"
                        bordered={false}
                        style={{ height: '100%' }}
                        loading={payments.isLoading}
                    >
                        <Table
                            dataSource={payments.data?.paymentsByMethod || []}
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
                <Col span={12}>
                    <Card
                        title="Top-Kunden"
                        bordered={false}
                        style={{ height: '100%' }}
                        loading={customers.isLoading}
                    >
                        <Table
                            dataSource={(customers.data?.topCustomers ?? []).slice(0, 5)}
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
        </div>
    );
}
