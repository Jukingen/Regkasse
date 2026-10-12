'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { ExtensionManager } from '@/features/extensions/ExtensionManager';
import { useI18n } from '@/i18n';

export default function AdminExtensionsPage() {
  const { t } = useI18n();
  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.extensions.title')} subtitle={t('admin.extensions.subtitle')} />
      <ExtensionManager />
    </AdminPageShell>
  );
}
