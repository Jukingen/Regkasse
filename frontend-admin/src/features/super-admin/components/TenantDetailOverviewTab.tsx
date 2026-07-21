'use client';

import { CreditCardOutlined, FilePdfOutlined } from '@ant-design/icons';
import { Button, Card, Descriptions, Popconfirm, Space, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { SkeletonWrapper } from '@/components/Skeleton';
import { useBillingTenantLicense } from '@/features/billing/hooks';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { TenantLicenseBadge } from '@/features/super-admin/components/TenantLicenseBadge';
import { tenantStatusColor } from '@/features/super-admin/utils/tenantStatusLabel';
import { buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';
import { formatDate, formatDateTime, useI18n } from '@/i18n';

export type TenantDetailOverviewTabProps = {
  tenant: AdminTenantDetail;
  suspendPending?: boolean;
  onSuspend: () => void;
  onReactivate: () => void;
};

export function TenantDetailOverviewTab({
  tenant,
  suspendPending,
  onSuspend,
  onReactivate,
}: TenantDetailOverviewTabProps) {
  const { t, formatLocale } = useI18n();
  const router = useRouter();
  const canAccessBilling = useBillingAccess();
  const { data: licenseInfo, isLoading: licenseLoading } = useBillingTenantLicense(
    tenant.id,
    canAccessBilling
  );
  const licenseStatus = licenseInfo?.status;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Card title={t('tenants.detail.overview.statusCard')}>
        <Descriptions column={{ xs: 1, sm: 2 }} size="small">
          <Descriptions.Item label={t('tenants.columns.status')}>
            <Tag color={tenantStatusColor(tenant.status)}>{tenant.status}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.detail.overview.created')}>
            {formatDate(tenant.createdAt, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.detail.overview.lastActivity')}>
            {tenant.lastActivityAtUtc
              ? formatDateTime(tenant.lastActivityAtUtc, formatLocale)
              : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.columns.adminUser')}>
            {tenant.ownerAdminEmail ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.columns.license')}>
            <TenantLicenseBadge
              tenantId={tenant.id}
              licenseValidUntilUtc={tenant.licenseValidUntilUtc}
              licenseKey={tenant.licenseKey}
              licenseDaysRemaining={tenant.licenseDaysRemaining}
            />
          </Descriptions.Item>
        </Descriptions>
        {tenant.status !== 'deleted' ? (
          <Space wrap style={{ marginTop: 16 }}>
            {tenant.status === 'active' ? (
              <Popconfirm
                title={t('tenants.detail.overview.confirmSuspend.title')}
                description={t('tenants.detail.overview.confirmSuspend.body')}
                onConfirm={onSuspend}
              >
                <Button loading={suspendPending}>{t('tenants.actions.suspend')}</Button>
              </Popconfirm>
            ) : (
              <Button loading={suspendPending} onClick={onReactivate}>
                {t('tenants.actions.reactivate')}
              </Button>
            )}
            <Link href={buildAdminUsersPageHref(tenant.id)}>
              <Button>{t('tenants.detail.overview.manageUsers')}</Button>
            </Link>
            <Link href={`/admin/tenants/${tenant.id}?tab=license`}>
              <Button>{t('tenants.actions.manageLicense')}</Button>
            </Link>
          </Space>
        ) : null}
      </Card>

      <Card title={t('tenants.detail.overview.statsTitle')}>
        <Descriptions column={{ xs: 1, sm: 3 }} size="small">
          <Descriptions.Item label={t('tenants.detail.tabs.users')}>
            {tenant.activeUserCount ?? 0}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.detail.tabs.registers')}>
            {tenant.cashRegisterCount ?? 0}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.fields.slug')}>
            <Typography.Text code>{tenant.slug}</Typography.Text>
          </Descriptions.Item>
        </Descriptions>
      </Card>

      {canAccessBilling ? (
        <Card title={t('license.tenantDetail.license')} size="small">
          <SkeletonWrapper type="form" loading={licenseLoading} count={4}>
            <Descriptions column={{ xs: 1, sm: 2 }} size="small">
              <Descriptions.Item label={t('license.tenantDetail.status')}>
                <Tag color={licenseStatus?.isValid ? 'green' : 'red'}>
                  {licenseStatus?.isValid
                    ? t('license.tenantDetail.active')
                    : t('license.tenantDetail.inactive')}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('license.tenantDetail.licenseKey')}>
                {licenseStatus?.licenseKey?.trim() ? (
                  <Typography.Text code style={{ fontSize: 12 }}>
                    {licenseStatus.licenseKey}
                  </Typography.Text>
                ) : (
                  '—'
                )}
              </Descriptions.Item>
              <Descriptions.Item label={t('license.tenantDetail.validUntil')}>
                {licenseStatus?.validUntilUtc
                  ? formatDate(licenseStatus.validUntilUtc, formatLocale, {
                      dateStyle: 'medium',
                    })
                  : '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('license.tenantDetail.daysRemaining')}>
                {licenseStatus?.daysRemaining != null
                  ? t('license.tenantDetail.daysRemainingValue', {
                      count: licenseStatus.daysRemaining,
                    })
                  : '—'}
              </Descriptions.Item>
            </Descriptions>
            <Space wrap style={{ marginTop: 12 }}>
              <Button
                size="small"
                icon={<CreditCardOutlined />}
                onClick={() => router.push(`/admin/billing/sales/new?tenantId=${tenant.id}`)}
              >
                {t('license.tenantDetail.sellLicense')}
              </Button>
              <Button
                size="small"
                icon={<FilePdfOutlined />}
                onClick={() => router.push(`/admin/billing/sales?tenantId=${tenant.id}`)}
              >
                {t('license.tenantDetail.viewSales')}
              </Button>
            </Space>
          </SkeletonWrapper>
        </Card>
      ) : null}

      {tenant.status === 'deleted' ? (
        <Typography.Paragraph type="secondary">
          {t('tenants.detail.settings.danger.deletedSettingsHint')}
        </Typography.Paragraph>
      ) : null}
    </Space>
  );
}
