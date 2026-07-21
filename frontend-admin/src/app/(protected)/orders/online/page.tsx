'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { OrderManagement } from '@/features/orders/components/OrderManagement';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function OnlineOrdersPage() {
  const { t } = useI18n();
  const breadcrumbs = [adminOverviewCrumb(t), { title: t('onlineOrders.pageTitle') }];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('onlineOrders.pageTitle')} breadcrumbs={breadcrumbs} />
      <p style={{ color: '#64748b', margin: 0 }}>{t('onlineOrders.pageSubtitle')}</p>
      <OrderManagement />
    </div>
  );
}
