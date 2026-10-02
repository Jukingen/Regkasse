'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { LodgingWorkspace } from '@/features/lodging/LodgingWorkspace';
import { useI18n } from '@/i18n';

export default function AdminRoomsPage() {
  const { t } = useI18n();
  return (
    <AdminPageShell>
      <AdminPageHeader title={t('tenants.lodging.title')} subtitle={t('tenants.lodging.subtitle')} />
      <LodgingWorkspace />
    </AdminPageShell>
  );
}
