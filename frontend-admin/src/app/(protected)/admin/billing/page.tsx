'use client';

import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, Col, Row, Space, Spin, Statistic, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { BillingAuditTable } from '@/features/billing/components/BillingAuditTable';
import { BillingExpiringTable } from '@/features/billing/components/BillingExpiringTable';
import { BillingSalesTable } from '@/features/billing/components/BillingSalesTable';
import { useBillingStats } from '@/features/billing/hooks';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function BillingOverviewPage() {
  const router = useRouter();
  const { t } = useI18n();

  const { data: stats, isLoading: statsLoading } = useBillingStats();

  const expiringSoon = stats?.expiringSoonLicenses ?? 0;

  return (
    <BillingAccessGate>
      <AdminPageShell>
        <AdminPageHeader
          title={t('billing.overview.pageTitle')}
          breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.licenseHubSales') }]}
          actions={
            <Space wrap>
              <Button onClick={() => router.push('/admin/billing/stats')}>
                {t('billing.overview.viewStats')}
              </Button>
              <Button onClick={() => router.push('/billing/digital')}>
                {t('billing.digital.viewDashboard')}
              </Button>
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => router.push('/admin/billing/sales/new')}
              >
                {t('billing.overview.newSale')}
              </Button>
            </Space>
          }
        />

        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
          <p style={{ color: '#64748b', margin: 0 }}>{t('billing.overview.pageSubtitle')}</p>

          <Spin spinning={statsLoading}>
            <Row gutter={[16, 16]}>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.totalRevenueNet')}
                    value={stats?.totalRevenueNet ?? 0}
                    precision={2}
                    prefix="€"
                  />
                </Card>
              </Col>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.totalRevenueGross')}
                    value={stats?.totalRevenueGross ?? 0}
                    precision={2}
                    prefix="€"
                  />
                </Card>
              </Col>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.activeLicenses')}
                    value={stats?.activeLicenses ?? 0}
                  />
                </Card>
              </Col>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.expiringSoon')}
                    value={expiringSoon}
                    styles={{ content: { color: expiringSoon > 5 ? '#dc2626' : '#eab308' } }}
                  />
                </Card>
              </Col>
            </Row>

            <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.totalSales')}
                    value={stats?.totalSales ?? 0}
                  />
                </Card>
              </Col>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.expiredLicenses')}
                    value={stats?.expiredLicenses ?? 0}
                    styles={{ content: { color: '#dc2626' } }}
                  />
                </Card>
              </Col>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.tenantsWithLicense')}
                    value={stats?.totalTenantsWithLicense ?? 0}
                  />
                </Card>
              </Col>
              <Col xs={24} sm={12} lg={6}>
                <Card variant="borderless">
                  <Statistic
                    title={t('billing.overview.avgPrice')}
                    value={stats?.averagePriceNet ?? 0}
                    precision={2}
                    prefix="€"
                  />
                </Card>
              </Col>
            </Row>
          </Spin>

          <div>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: 16,
                flexWrap: 'wrap',
                gap: 12,
              }}
            >
              <Typography.Title level={5} style={{ margin: 0 }}>
                {t('billing.sales.pageTitle')}
              </Typography.Title>
              <Button type="link" onClick={() => router.push('/admin/billing/sales')}>
                {t('billing.overview.viewSales')}
              </Button>
            </div>
            <BillingSalesTable showHeaderActions={false} />
          </div>

          <Card
            title={t('billing.overview.expiringTitle')}
            extra={
              <Button type="link" onClick={() => router.push('/admin/billing/sales')}>
                {t('billing.overview.viewAll')}
              </Button>
            }
          >
            <BillingExpiringTable />
          </Card>

          <Card title={t('billing.overview.auditTitle')}>
            <BillingAuditTable pageSize={10} />
          </Card>
        </Space>
      </AdminPageShell>
    </BillingAccessGate>
  );
}
