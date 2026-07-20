'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { PaymentGatewaySettingsForm } from '@/features/settings/components/PaymentGatewaySettingsForm';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function PaymentSettingsPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.paymentGateway.pageTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={t('settings.paymentGateway.pageTitle')}
        breadcrumbs={breadcrumbs}
      />
      <PaymentGatewaySettingsForm />
    </div>
  );
}
