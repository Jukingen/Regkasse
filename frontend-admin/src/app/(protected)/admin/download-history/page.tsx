'use client';

import { Col, Row, Space } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { SensitiveExportApprovalsPanel } from '@/components/ui/SensitiveExportApprovalsPanel';
import { DownloadHistoryPanel } from '@/features/download-history/components/DownloadHistoryPanel';
import { ExportEmailDeliveryHistoryPanel } from '@/features/export-email/components/ExportEmailDeliveryHistoryPanel';
import { ExportQuickActionsCard } from '@/features/exports/components/ExportQuickActionsCard';
import { ExportTypesPanel } from '@/features/exports/components/ExportTypesPanel';
import { ExportTemplatesPanel } from '@/features/exports/components/ExportTemplatesPanel';
import { RecentExportsList } from '@/features/exports/components/RecentExportsList';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function DownloadHistoryPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('common.downloadHistory.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('adminShell.group.rksv'), href: '/rksv' },
          { title: t('common.downloadHistory.breadcrumb') },
        ]}
      />
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={12}>
            <ExportQuickActionsCard />
          </Col>
          <Col xs={24} lg={12}>
            <RecentExportsList />
          </Col>
        </Row>
        <ExportTypesPanel />
        <ExportTemplatesPanel />
        <SensitiveExportApprovalsPanel />
        <ExportEmailDeliveryHistoryPanel />
        <DownloadHistoryPanel />
      </Space>
    </AdminPageShell>
  );
}
