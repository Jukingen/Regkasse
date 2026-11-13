'use client';

/**
 * FA working-hours management — full CRUD for schedule / special days / online cutoff.
 * Never restricts Admin access or operations based on open/closed status.
 * Online-order blocking applies only on customer website/app surfaces.
 */
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { WorkingHoursSettingsForm } from '@/features/settings/components/WorkingHoursSettingsForm';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function WorkingHoursSettingsPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.workingHours.pageTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('settings.workingHours.pageTitle')} breadcrumbs={breadcrumbs} />
      <WorkingHoursSettingsForm />
    </div>
  );
}
