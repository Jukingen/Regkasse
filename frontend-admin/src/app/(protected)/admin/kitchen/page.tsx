'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { KitchenWorkspace } from '@/features/kitchen/KitchenWorkspace';
import { useI18n } from '@/i18n';

export default function AdminKitchenPage() {
  const { t } = useI18n();
  return (
    <AdminPageShell>
      <AdminPageHeader title={t('tenants.kitchen.title')} subtitle={t('tenants.kitchen.subtitle')} />
      <KitchenWorkspace />
    </AdminPageShell>
  );
}
