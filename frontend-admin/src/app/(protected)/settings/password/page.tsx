'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ChangeMyPasswordForm } from '@/features/settings/components/ChangeMyPasswordForm';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function PasswordChangePage() {
  const { t } = useI18n();
  const pageTitle = t('settings.changePassword.title');
  const breadcrumbs = [adminOverviewCrumb(t), { title: pageTitle }];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
      <ChangeMyPasswordForm />
    </div>
  );
}
