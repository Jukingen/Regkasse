'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { Alert, Button } from 'antd';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { OrderManagement } from '@/features/orders/components/OrderManagement';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

/**
 * Mandanten-Admin online-order inbox for a tenant context.
 * Uses ambient JWT tenant scope (same as `/orders/online`).
 * Path: `/tenant/[id]/orders`
 */
export default function TenantOnlineOrdersPage() {
  const { t } = useI18n();
  const params = useParams();
  const { user } = useAuth();
  const tenantId = typeof params.id === 'string' ? params.id : '';

  const canAccess =
    isSuperAdmin(user?.role) ||
    hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL) ||
    hasPermission(user, PERMISSIONS.DIGITAL_ORDERS_VIEW) ||
    hasPermission(user, PERMISSIONS.ORDER_VIEW);

  if (!canAccess) {
    return (
      <AdminPageShell>
        <Alert
          type="error"
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </AdminPageShell>
    );
  }

  if (!tenantId) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('onlineOrders.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('tenants.digitalServices.pageTitle'), href: `/tenant/${tenantId}/digital` },
          { title: t('onlineOrders.pageTitle') },
        ]}
        actions={
          <Link href={`/tenant/${tenantId}/digital`}>
            <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
          </Link>
        }
      />
      <p style={{ color: 'var(--ant-color-text-secondary)', margin: '0 0 16px' }}>
        {t('onlineOrders.pageSubtitle')}
      </p>
      <OrderManagement />
    </AdminPageShell>
  );
}
