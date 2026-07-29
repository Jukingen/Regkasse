'use client';

import { DeleteOutlined, DownloadOutlined, FolderOpenOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Row, Typography } from 'antd';
import Link from 'next/link';
import { useState } from 'react';

import { DataDeletionRequestModal } from '@/features/data-management/components/DataDeletionRequestModal';
import {
  useCreateDataRightsRequest,
  useDataRightsRequests,
  useDownloadDataRightsExport,
  useTenantDataManagementSummary,
} from '@/features/data-management/hooks/useTenantDataManagement';
import { buildDataExportFileName } from '@/features/data-management/utils/dataExportFileName';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { usePermissions } from '@/hooks/usePermissions';
import { useSensitiveExportGate } from '@/hooks/useSensitiveExportGate';
import { useI18n } from '@/i18n';
import { SENSITIVE_EXPORT_KINDS } from '@/lib/download/sensitiveExportSecurity';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Paragraph, Text } = Typography;

type Props = {
  /** Optional override; defaults to current tenant from context. */
  tenantId?: string;
};

/**
 * Compact GDPR export / deletion actions for mandant admins whose license is locked or archived.
 * Uses the same data-rights APIs as `/settings/data-management` / `/tenant/[id]/data-management`.
 */
export function LockedLicenseDataRightsCard({ tenantId: tenantIdProp }: Props) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const tenant = useCurrentTenant();
  const tenantId = tenantIdProp ?? tenant.tenantId ?? '';
  const { isSuperAdmin } = usePermissions();
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.BACKUP_MANAGE,
  });

  const summaryQuery = useTenantDataManagementSummary(tenantId);
  const requestsQuery = useDataRightsRequests(tenantId);
  const createMutation = useCreateDataRightsRequest(tenantId);
  const downloadMutation = useDownloadDataRightsExport(tenantId);
  const sensitiveGate = useSensitiveExportGate();
  const [deletionOpen, setDeletionOpen] = useState(false);

  if (!tenant.isRealTenantSlug || !tenantId || !isAuthorized) {
    return null;
  }

  const canExport = summaryQuery.data?.canExport !== false;
  const canRequestDeletion = summaryQuery.data?.canRequestDeletion === true;

  const dataManagementHref = `/settings/account`;

  const downloadReadyExport = async (requestId: string, artifactFileName?: string | null) => {
    sensitiveGate.run({
      kind: SENSITIVE_EXPORT_KINDS.GdprDataExport,
      resourceId: requestId,
      isSuperAdmin,
      execute: async (headers) => {
        const blob = await downloadMutation.mutateAsync({ requestId, headers });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download =
          artifactFileName ??
          buildDataExportFileName(summaryQuery.data?.tenantSlug ?? null);
        a.click();
        URL.revokeObjectURL(url);
        message.success(t('dataManagement.exportSuccess'));
      },
    });
  };

  const requestDataExport = async () => {
    if (!canExport) {
      message.warning(t('dataManagement.lockedCard.exportUnavailable'));
      return;
    }

    try {
      const row = await createMutation.mutateAsync({ type: 'export' });
      message.success(t('dataManagement.requestSent'));

      if (row.canDownload) {
        await downloadReadyExport(row.id, row.artifactFileName);
      } else if (row.downloadLink) {
        message.success(t('dataManagement.ready'));
        window.open(row.downloadLink, '_blank', 'noopener,noreferrer');
      } else {
        message.info(t('dataManagement.processing'));
      }
      void requestsQuery.refetch();
    } catch {
      message.error(t('dataManagement.rights.requestFailed'));
    }
  };

  const requestDeletion = () => {
    if (!canRequestDeletion) {
      message.warning(t('dataManagement.deleteWarning'));
      return;
    }
    setDeletionOpen(true);
  };

  return (
    <>
      <Card
        title={t('dataManagement.lockedCard.title')}
        style={{ marginTop: 16 }}
        extra={
          <Link href={dataManagementHref}>
            <Button type="link" icon={<FolderOpenOutlined />} style={{ paddingInline: 0 }}>
              {t('dataManagement.lockedCard.openFull')}
            </Button>
          </Link>
        }
      >
        <Paragraph type="secondary" style={{ marginBottom: 16 }}>
          {t('dataManagement.lockedCard.intro')}
        </Paragraph>

        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('dataManagement.rksvTitle')}
          description={t('dataManagement.rksvNote')}
        />

        <Row gutter={[16, 16]}>
          <Col xs={24} md={12}>
            <Card size="small" type="inner" title={t('dataManagement.lockedCard.exportTitle')}>
              <Paragraph type="secondary" style={{ minHeight: 44 }}>
                {t('dataManagement.lockedCard.exportDescription')}
              </Paragraph>
              <Button
                type="primary"
                icon={<DownloadOutlined />}
                loading={createMutation.isPending || downloadMutation.isPending}
                disabled={!canExport}
                onClick={() => void requestDataExport()}
              >
                {t('dataManagement.lockedCard.exportButton')}
              </Button>
            </Card>
          </Col>
          <Col xs={24} md={12}>
            <Card size="small" type="inner" title={t('dataManagement.lockedCard.deleteTitle')}>
              <Paragraph type="secondary" style={{ minHeight: 44 }}>
                {t('dataManagement.lockedCard.deleteDescription')}
              </Paragraph>
              <Button
                danger
                icon={<DeleteOutlined />}
                disabled={!canRequestDeletion}
                onClick={requestDeletion}
              >
                {t('dataManagement.lockedCard.deleteButton')}
              </Button>
              {!canRequestDeletion ? (
                <Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                  {t('dataManagement.deleteWarning')}
                </Text>
              ) : null}
            </Card>
          </Col>
        </Row>
      </Card>

      <DataDeletionRequestModal
        tenantId={tenantId}
        open={deletionOpen}
        onClose={() => setDeletionOpen(false)}
      />
    </>
  );
}
