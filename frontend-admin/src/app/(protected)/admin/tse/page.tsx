'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { TenantDataExportButton } from '@/features/data-export/TenantDataExportButton';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

/** Super Admin TSE signature metadata export. The file has no JWS payload. */
export default function AdminTseSignatureExportPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  return (
    <AdminPageHeader
      title={t('nav.adminTseManagement')}
      breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.adminTseManagement') }]}
      actions={canExport ? <TenantDataExportButton type="tse_signatures" /> : null}
    />
  );
}
