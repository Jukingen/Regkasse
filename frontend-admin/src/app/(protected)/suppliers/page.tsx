'use client';

import { Alert, Typography } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { TenantDataExportButton } from '@/features/data-export/TenantDataExportButton';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

/** Supplier list download. Rows come from GET /api/admin/export/suppliers. */
export default function SuppliersPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();

  if (!hasPermission(PERMISSIONS.SUPPLIER_VIEW)) {
    return (
      <Alert
        type="error"
        title={t('common.dataExport.suppliersDenied')}
        style={{ margin: 24 }}
      />
    );
  }

  return (
    <AdminPageHeader
      title={t('nav.suppliers')}
      breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.suppliers') }]}
      actions={<TenantDataExportButton type="suppliers" />}
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('common.dataExport.suppliersIntro')}
      </Typography.Paragraph>
    </AdminPageHeader>
  );
}
