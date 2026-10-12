'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { FolioList } from '@/features/lodging/FolioList';
import { useI18n } from '@/i18n';

export default function AdminFoliosPage() {
  const { t } = useI18n();
  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.folios.title')} subtitle={t('admin.folios.subtitle')} />
      <FolioList />
    </AdminPageShell>
  );
}
