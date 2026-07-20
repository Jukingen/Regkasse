'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DigitalServiceAccess } from '@/features/digital/components/DigitalServiceAccess';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { Typography } from 'antd';

const { Paragraph } = Typography;

/**
 * Mandanten-Admin customer portal: digital service pricing + one-click generators (ambient tenant).
 */
export default function DigitalCustomerPortalPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('tenants.digitalServices.portalTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('tenants.digitalServices.portalTitle')} breadcrumbs={breadcrumbs} />
      <Paragraph type="secondary">{t('tenants.digitalServices.portalSubtitle')}</Paragraph>
      <DigitalServiceAccess />
    </div>
  );
}
