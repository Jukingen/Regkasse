'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { HairSalonCampaignEditor } from '@/features/extensions/HairSalonCampaignEditor';
import { useI18n } from '@/i18n';

export default function HairSalonCampaignsPage() {
  const { t } = useI18n();
  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('admin.hairSalonCampaigns.title')}
        subtitle={t('admin.hairSalonCampaigns.subtitle')}
        breadcrumbs={[
          { title: t('nav.settings.title'), href: '/settings' },
          { title: t('nav.extensions'), href: '/admin/extensions' },
          { title: t('admin.hairSalonCampaigns.title') },
        ]}
      />
      <HairSalonCampaignEditor />
    </AdminPageShell>
  );
}
