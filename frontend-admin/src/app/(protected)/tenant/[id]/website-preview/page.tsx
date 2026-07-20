'use client';

import { Alert, Button } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { DigitalServiceAccess } from '@/features/digital/components/DigitalServiceAccess';
import { WebsiteTemplatePreviewPanel } from '@/features/website-generator/components/WebsiteTemplatePreviewPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

/**
 * Mandanten-Admin: read-only website template preview + template-change request.
 * Path: `/tenant/[id]/website-preview`
 */
export default function WebsitePreviewPage() {
  const { t } = useI18n();
  const params = useParams();
  const tenantId = typeof params.id === 'string' ? params.id : '';

  if (!tenantId) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <DigitalServiceAccess tenantId={tenantId} blockWhenDisabled={false}>
        <AdminPageHeader
          title={t('tenants.websitePreview.pageTitle')}
          breadcrumbs={[
            adminOverviewCrumb(t),
            { title: t('tenants.digitalServices.pageTitle'), href: `/tenant/${tenantId}/digital` },
            { title: t('tenants.websitePreview.pageTitle') },
          ]}
          actions={
            <Link href={`/tenant/${tenantId}/digital`}>
              <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
            </Link>
          }
        />
        <WebsiteTemplatePreviewPanel tenantId={tenantId} />
      </DigitalServiceAccess>
    </AdminPageShell>
  );
}
