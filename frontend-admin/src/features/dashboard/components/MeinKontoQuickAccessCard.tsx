'use client';

import {
  ArrowRightOutlined,
  CustomerServiceOutlined,
  FileTextOutlined,
  KeyOutlined,
} from '@ant-design/icons';
import { Badge, Card, Space, Tag, Typography } from 'antd';
import Link from 'next/link';

import {
  fetchOpenSupportTicketCount,
  supportTicketQueryKeys,
} from '@/features/support-tickets/api/supportTickets';
import {
  fetchTenantInvoices,
  tenantInvoiceQueryKeys,
} from '@/features/tenant-invoices/api/tenantInvoices';
import {
  portalLicenseStatusColor,
  portalLicenseStatusLabelKey,
} from '@/features/tenant-portal/utils/tenantPortalDisplay';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

export function MeinKontoQuickAccessCard() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const { status } = useLicenseStatus();

  const canOpenPortal = hasPermission(PERMISSIONS.LICENSE_MANAGE);

  const invoicesQuery = useAuthorizedQuery({
    queryKey: tenantInvoiceQueryKeys.list({}),
    queryFn: ({ signal }) => fetchTenantInvoices({}, signal),
    requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
    enabled: canOpenPortal,
  });

  const supportQuery = useAuthorizedQuery({
    queryKey: supportTicketQueryKeys.openCount(),
    queryFn: ({ signal }) => fetchOpenSupportTicketCount(signal),
    requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
    enabled: canOpenPortal,
  });

  if (!canOpenPortal) return null;

  const invoiceCount = invoicesQuery.data?.totalCount ?? 0;
  const openTickets = supportQuery.data?.openCount ?? 0;

  return (
    <Card
      title={t('tenantPortal.portal.title')}
      extra={
        <Link href="/tenant/portal">
          {t('tenantPortal.portal.openHub')} <ArrowRightOutlined aria-hidden />
        </Link>
      }
      style={{ marginBottom: 16 }}
    >
      <Space orientation="vertical" size={12} style={{ width: '100%' }}>
        <Space wrap>
          <KeyOutlined aria-hidden />
          <Typography.Text>{t('tenantPortal.portal.licenseStatus')}</Typography.Text>
          <Tag color={portalLicenseStatusColor(status?.state)}>
            {t(portalLicenseStatusLabelKey(status?.state))}
          </Tag>
        </Space>
        <Space>
          <FileTextOutlined aria-hidden />
          <Typography.Text>{t('tenantPortal.portal.invoices')}</Typography.Text>
          <Badge
            count={invoiceCount}
            showZero
            overflowCount={999}
            aria-label={t('tenantPortal.portal.totalInvoices', { count: invoiceCount })}
          />
        </Space>
        <Space>
          <CustomerServiceOutlined aria-hidden />
          <Link href="/tenant/support">{t('tenantPortal.portal.goSupport')}</Link>
          <Badge
            count={openTickets}
            showZero
            overflowCount={999}
            aria-label={t('tenantPortal.portal.openTickets', { count: openTickets })}
          />
        </Space>
      </Space>
    </Card>
  );
}
