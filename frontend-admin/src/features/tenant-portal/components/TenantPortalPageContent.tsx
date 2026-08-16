'use client';

import {
  ArrowRightOutlined,
  CustomerServiceOutlined,
  FileTextOutlined,
  IdcardOutlined,
  KeyOutlined,
} from '@ant-design/icons';
import { Badge, Card, Col, Row, Space, Statistic, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useMemo } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
  fetchTenantInvoices,
  tenantInvoiceQueryKeys,
} from '@/features/tenant-invoices/api/tenantInvoices';
import {
  fetchTenantOnboarding,
  tenantOnboardingQueryKeys,
} from '@/features/tenant-portal/api/onboarding';
import {
  fetchOpenSupportTicketCount,
  supportTicketQueryKeys,
} from '@/features/support-tickets/api/supportTickets';
import {
  isPortalProfileComplete,
  portalLicenseDaysCopy,
  portalLicenseStatusColor,
  portalLicenseStatusLabelKey,
  portalOpenInvoiceCount,
  resolvePortalDisplayName,
} from '@/features/tenant-portal/utils/tenantPortalDisplay';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export function TenantPortalPageContent() {
  const { t } = useI18n();
  const { user } = useAuth();
  const tenant = useCurrentTenant();
  const { status } = useLicenseStatus();

  const displayName = resolvePortalDisplayName(user?.firstName, user?.lastName, user?.userName);
  const tenantId = tenant.tenantId;

  const invoicesQuery = useAuthorizedQuery({
    queryKey: tenantInvoiceQueryKeys.list({}),
    queryFn: ({ signal }) => fetchTenantInvoices({}, signal),
    requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
  });

  const onboardingQuery = useAuthorizedQuery({
    queryKey: tenantOnboardingQueryKeys.byTenant(tenantId ?? ''),
    queryFn: ({ signal }) => fetchTenantOnboarding(tenantId!, signal),
    requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
    enabled: !!tenantId,
  });

  const supportQuery = useAuthorizedQuery({
    queryKey: supportTicketQueryKeys.openCount(),
    queryFn: ({ signal }) => fetchOpenSupportTicketCount(signal),
    requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
  });

  const daysCopy = portalLicenseDaysCopy(status);
  const openInvoices = portalOpenInvoiceCount(invoicesQuery.data ?? null);
  const profileComplete = isPortalProfileComplete(onboardingQuery.data ?? null);

  const pageTitle = t('tenantPortal.portal.title');
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.meinKonto') },
  ];

  const quickLinks = useMemo(
    () => [
      {
        href: '/license/dashboard',
        label: t('tenantPortal.portal.goLicense'),
      },
      {
        href: '/tenant/invoices',
        label: t('tenantPortal.portal.goInvoices'),
      },
      {
        href: '/profile',
        label: t('tenantPortal.portal.goProfile'),
      },
      {
        href: '/tenant/support',
        label: t('tenantPortal.portal.goSupport'),
      },
    ],
    [t]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={pageTitle}
        breadcrumbs={breadcrumbs}
        subtitle={t('tenantPortal.portal.welcome', { name: displayName })}
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <Card title={t('tenantPortal.portal.licenseStatus')} extra={<KeyOutlined />}>
            <Space orientation="vertical" size={8} style={{ width: '100%' }}>
              <Tag color={portalLicenseStatusColor(status?.state)}>
                {t(portalLicenseStatusLabelKey(status?.state))}
              </Tag>
              {daysCopy ? (
                <Typography.Text type="secondary">
                  {t(daysCopy.key, { days: daysCopy.days })}
                </Typography.Text>
              ) : null}
            </Space>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card title={t('tenantPortal.portal.invoices')} extra={<FileTextOutlined />}>
            <Space orientation="vertical" size={4} style={{ width: '100%' }}>
              <Typography.Text>
                {t('tenantPortal.portal.totalInvoices', {
                  count: invoicesQuery.data?.totalCount ?? 0,
                })}
              </Typography.Text>
              <Typography.Text type="secondary">
                {t('tenantPortal.portal.openInvoices', { count: openInvoices })}
              </Typography.Text>
            </Space>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card title={t('tenantPortal.portal.profile')} extra={<IdcardOutlined />}>
            <Statistic
              title={
                profileComplete
                  ? t('tenantPortal.portal.profileComplete')
                  : t('tenantPortal.portal.profileIncomplete')
              }
              value={
                onboardingQuery.data
                  ? Math.round(
                      (onboardingQuery.data.completedCount /
                        Math.max(1, onboardingQuery.data.totalCount)) *
                        100
                    )
                  : 0
              }
              suffix="%"
              loading={onboardingQuery.isLoading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card title={t('tenantPortal.portal.support')} extra={<CustomerServiceOutlined />}>
            <Space orientation="vertical" size={8} style={{ width: '100%' }}>
              <Badge
                count={supportQuery.data?.openCount ?? 0}
                showZero
                overflowCount={999}
                color="blue"
              />
              <Typography.Text type="secondary">
                {t('tenantPortal.portal.openTickets', {
                  count: supportQuery.data?.openCount ?? 0,
                })}
              </Typography.Text>
              <Link href="/tenant/support">{t('tenantPortal.portal.goSupport')}</Link>
            </Space>
          </Card>
        </Col>
      </Row>

      <Card title={t('tenantPortal.portal.quickLinks')}>
        <Row gutter={[16, 16]}>
          {quickLinks.map((link) => (
            <Col xs={24} sm={12} md={6} key={link.href}>
              <Link
                href={link.href}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  fontWeight: 500,
                }}
              >
                <span>{link.label}</span>
                <ArrowRightOutlined aria-hidden style={{ opacity: 0.55 }} />
              </Link>
            </Col>
          ))}
        </Row>
      </Card>
    </div>
  );
}
