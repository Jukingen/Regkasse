'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { PreorderList } from '@/features/orders/components/PreorderList';
import { PreorderSettingsForm } from '@/features/orders/components/PreorderSettingsForm';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function PreordersPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const breadcrumbs = [adminOverviewCrumb(t), { title: t('onlineOrders.preorder.pageTitle') }];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('onlineOrders.preorder.pageTitle')} breadcrumbs={breadcrumbs} />
      <p style={{ color: '#64748b', margin: 0 }}>{t('onlineOrders.preorder.pageSubtitle')}</p>
      <PreorderList />
      {hasPermission(PERMISSIONS.SETTINGS_VIEW) ? <PreorderSettingsForm /> : null}
    </div>
  );
}
