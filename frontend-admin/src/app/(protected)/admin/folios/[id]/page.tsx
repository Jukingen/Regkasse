'use client';

import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { FolioDetail } from '@/features/lodging/FolioDetail';
import { useI18n } from '@/i18n';

export default function AdminFolioDetailPage() {
  const { t } = useI18n();
  const params = useParams<{ id: string }>();
  const folioId = typeof params.id === 'string' ? params.id : '';
  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.folios.detailTitle')} subtitle={t('admin.folios.subtitle')} />
      <FolioDetail folioId={folioId} />
    </AdminPageShell>
  );
}
