'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { TenantDataExportButton } from '@/features/data-export/TenantDataExportButton';
import { OrderManagement } from '@/features/orders/components/OrderManagement';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export default function OnlineOrdersPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const breadcrumbs = [adminOverviewCrumb(t), { title: t('onlineOrders.pageTitle') }];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={t('onlineOrders.pageTitle')}
        breadcrumbs={breadcrumbs}
        actions={
          hasPermission(PERMISSIONS.ORDER_VIEW) ? <TenantDataExportButton type="orders" /> : null
        }
      />
      <p style={{ color: '#64748b', margin: 0 }}>{t('onlineOrders.pageSubtitle')}</p>
      <OrderManagement />
    </div>
  );
}
