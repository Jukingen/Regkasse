'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Card,
  Col,
  Empty,
  List,
  Row,
  Select,
  Statistic,
  Typography,
} from 'antd';
import { useMemo, useState } from 'react';
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  getTseSustainabilityOptimizations,
  getTseSustainabilityReport,
} from '@/features/tse-sustainability/api/sustainability';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-sustainability'] as const;

export default function TseSustainabilityPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-sustainability'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const reportQuery = useQuery({
    queryKey: [...KEY, 'report', tenantId],
    queryFn: ({ signal }) => getTseSustainabilityReport(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const optimizationsQuery = useQuery({
    queryKey: [...KEY, 'optimizations', tenantId],
    queryFn: ({ signal }) => getTseSustainabilityOptimizations(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const report = reportQuery.data;
  const carbonTrend = useMemo(
    () =>
      (report?.carbonTrend ?? []).map((row) => ({
        label: row.label,
        carbonKg: row.carbonKg,
      })),
    [report?.carbonTrend]
  );

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseSustainability.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseSustainability.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseSustainability.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseSustainability.tenantLabel')}
            loading={tenantsQuery.isLoading}
            value={tenantId}
            onChange={setTenantId}
            options={(tenantsQuery.data ?? []).map((tenant) => ({
              value: tenant.id,
              label: `${tenant.name} (${tenant.slug})`,
            }))}
          />
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseSustainability.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseSustainability.emptySelect')} />
      ) : reportQuery.isError ? (
        <Alert type="error" showIcon message={t('tseSustainability.loadError')} />
      ) : (
        <>
          <Card title={t('tseSustainability.cardTitle')} loading={reportQuery.isLoading}>
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              message={t('tseSustainability.diagnosticNote')}
            />

            <Row gutter={16}>
              <Col xs={24} sm={8}>
                <Statistic
                  title={t('tseSustainability.totalCo2')}
                  value={report?.totalCarbonEmission ?? 0}
                  precision={3}
                  suffix={t('tseSustainability.kgCo2')}
                  valueStyle={{ color: '#52c41a' }}
                />
              </Col>
              <Col xs={24} sm={8}>
                <Statistic
                  title={t('tseSustainability.energySaved')}
                  value={report?.energySaved ?? 0}
                  precision={2}
                  suffix={t('tseSustainability.kwh')}
                />
              </Col>
              <Col xs={24} sm={8}>
                <Statistic
                  title={t('tseSustainability.costSaved')}
                  value={report?.costSaved ?? 0}
                  precision={2}
                  prefix="€"
                  valueStyle={{ color: '#1890ff' }}
                />
              </Col>
            </Row>

            <Card size="small" title={t('tseSustainability.carbonTrend')} style={{ marginTop: 16 }}>
              {carbonTrend.length === 0 ? (
                <Empty />
              ) : (
                <div style={{ width: '100%', height: 260 }}>
                  <ResponsiveContainer>
                    <AreaChart data={carbonTrend}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="label" tick={{ fontSize: 11 }} minTickGap={24} />
                      <YAxis tick={{ fontSize: 11 }} width={48} />
                      <Tooltip />
                      <Area
                        type="monotone"
                        dataKey="carbonKg"
                        stroke="#52c41a"
                        fill="#52c41a33"
                        name={t('tseSustainability.totalCo2')}
                      />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
              )}
            </Card>

            <Alert
              type="info"
              showIcon
              style={{ marginTop: 16 }}
              message={t('tseSustainability.percentileMessage').replace(
                '{percentile}',
                String(report?.percentile ?? 0)
              )}
            />
          </Card>

          <Card
            title={t('tseSustainability.optimizationsTitle')}
            loading={optimizationsQuery.isLoading}
            style={{ marginTop: 16 }}
          >
            <List
              dataSource={optimizationsQuery.data?.suggestions ?? []}
              locale={{ emptyText: t('tseSustainability.noOptimizations') }}
              renderItem={(item) => (
                <List.Item>
                  <List.Item.Meta
                    title={item.title}
                    description={
                      <>
                        <div>{item.description}</div>
                        <Typography.Text type="secondary">
                          {t('tseSustainability.colCarbon')}: {item.estimatedCarbonSavedKgPerMonth} ·{' '}
                          {t('tseSustainability.colEnergy')}: {item.estimatedEnergySavedKwhPerMonth} ·{' '}
                          {t('tseSustainability.colCost')}: €{item.estimatedCostSavedEurPerMonth}
                        </Typography.Text>
                      </>
                    }
                  />
                </List.Item>
              )}
            />
          </Card>
        </>
      )}
    </>
  );
}
