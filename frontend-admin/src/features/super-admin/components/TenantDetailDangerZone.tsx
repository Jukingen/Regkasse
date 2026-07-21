'use client';

import { DeleteOutlined, StopOutlined, UndoOutlined, WarningOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Modal, Space, Tooltip, Typography } from 'antd';
import Link from 'next/link';
import React, { useCallback, useState } from 'react';

import { isDevelopment } from '@/features/auth/services/devTenant';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { TenantArchiveConfirmModal } from '@/features/super-admin/components/TenantArchiveConfirmModal';
import { TenantPermanentDeleteModal } from '@/features/super-admin/components/TenantPermanentDeleteModal';
import { useTenantDeleteDependencies } from '@/features/super-admin/hooks/useTenantDeleteDependencies';
import {
  buildTenantDeletePreparationHref,
  resolveTenantDeleteFailureMessage,
} from '@/features/super-admin/utils/tenantDeleteDependencyUi';
import { useI18n } from '@/i18n';

export type TenantDetailDangerZoneProps = {
  tenant: AdminTenantDetail;
  restorePending?: boolean;
  developmentHardDeletePending?: boolean;
  onArchiveSuccess: () => void;
  onPermanentDeleteSuccess: () => void;
  onRestore: () => void | Promise<void>;
  onDevelopmentHardDelete?: () => void | Promise<void>;
};

function buildTenantAuditLogsHref(tenantId: string): string {
  const qp = new URLSearchParams({ entityType: 'Tenant', entityId: tenantId });
  return `/audit-logs?${qp.toString()}`;
}

export function TenantDetailDangerZone({
  tenant,
  restorePending,
  developmentHardDeletePending,
  onArchiveSuccess,
  onPermanentDeleteSuccess,
  onRestore,
  onDevelopmentHardDelete,
}: TenantDetailDangerZoneProps) {
  const { t } = useI18n();
  const [archiveOpen, setArchiveOpen] = useState(false);
  const [restoreOpen, setRestoreOpen] = useState(false);
  const [permanentDeleteOpen, setPermanentDeleteOpen] = useState(false);
  const [developmentHardDeleteOpen, setDevelopmentHardDeleteOpen] = useState(false);

  const isDeleted = tenant.status === 'deleted';
  const showDevelopmentHardDelete =
    isDevelopment() && typeof onDevelopmentHardDelete === 'function';

  const deleteDependenciesQuery = useTenantDeleteDependencies(tenant.id, isDeleted);
  const canHardDelete = deleteDependenciesQuery.data?.canHardDelete === true;
  const hardDeleteBlockedReason = resolveTenantDeleteFailureMessage(
    t,
    deleteDependenciesQuery.data?.failureCode,
    deleteDependenciesQuery.data?.failureMessage
  );

  const closeDevelopmentHardDelete = useCallback(() => {
    setDevelopmentHardDeleteOpen(false);
  }, []);

  const permanentDeleteButton = (
    <Button danger icon={<DeleteOutlined />} onClick={() => setPermanentDeleteOpen(true)}>
      {t('tenants.actions.hardDelete')}
    </Button>
  );

  const permanentDeleteControl =
    !canHardDelete && !deleteDependenciesQuery.isLoading ? (
      <Tooltip title={hardDeleteBlockedReason}>{permanentDeleteButton}</Tooltip>
    ) : (
      permanentDeleteButton
    );

  return (
    <Card
      id="danger-zone"
      title={
        <Space>
          <WarningOutlined style={{ color: '#cf1322' }} />
          {t('tenants.detail.danger.title')}
        </Space>
      }
      style={{
        borderColor: '#ff4d4f',
        marginTop: 24,
      }}
      styles={{ header: { borderBottomColor: '#ffccc7' } }}
    >
      {isDeleted ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert type="error" showIcon title={t('tenants.detail.settings.danger.deletedWarning')} />
          <Space wrap>
            <Link href={buildTenantDeletePreparationHref(tenant.id)}>
              <Button>{t('tenants.deleteDependencies.checkDependencies')}</Button>
            </Link>
            <Button
              icon={<UndoOutlined />}
              loading={restorePending}
              onClick={() => setRestoreOpen(true)}
            >
              {t('tenants.detail.settings.danger.restoreButton')}
            </Button>
            {permanentDeleteControl}
          </Space>
        </Space>
      ) : (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('tenants.detail.settings.danger.softDeleteHint')}
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('tenants.detail.settings.danger.decommissionWizardHint')}
          </Typography.Paragraph>
          {showDevelopmentHardDelete ? (
            <Alert
              type="warning"
              showIcon
              title={t('tenants.detail.settings.danger.developmentHardDeleteHint')}
            />
          ) : null}
          <Space wrap>
            <Link href={buildTenantDeletePreparationHref(tenant.id)}>
              <Button>{t('tenants.deleteDependencies.checkDependencies')}</Button>
            </Link>
            <Link href={`/admin/tenants/${tenant.id}/decommission`}>
              <Button icon={<StopOutlined />}>
                {t('tenants.detail.settings.danger.decommissionWizardButton')}
              </Button>
            </Link>
            <Button type="primary" danger onClick={() => setArchiveOpen(true)}>
              {t('tenants.detail.settings.danger.archiveButton')}
            </Button>
            {showDevelopmentHardDelete ? (
              <Button
                danger
                icon={<DeleteOutlined />}
                loading={developmentHardDeletePending}
                onClick={() => setDevelopmentHardDeleteOpen(true)}
              >
                {t('tenants.detail.settings.danger.developmentHardDeleteButton')}
              </Button>
            ) : null}
          </Space>
        </Space>
      )}

      <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
        <Link href={buildTenantAuditLogsHref(tenant.id)}>
          {t('tenants.detail.settings.danger.auditLink')}
        </Link>
      </Typography.Paragraph>

      <TenantArchiveConfirmModal
        open={archiveOpen}
        tenantId={tenant.id}
        tenantName={tenant.name}
        onClose={() => setArchiveOpen(false)}
        onSuccess={onArchiveSuccess}
      />

      <TenantPermanentDeleteModal
        open={permanentDeleteOpen}
        tenantId={tenant.id}
        tenantName={tenant.name}
        tenantSlug={tenant.slug}
        onClose={() => setPermanentDeleteOpen(false)}
        onSuccess={onPermanentDeleteSuccess}
      />

      <Modal
        title={t('tenants.detail.settings.danger.restoreModalTitle')}
        open={restoreOpen}
        onCancel={() => setRestoreOpen(false)}
        okText={t('tenants.detail.settings.danger.restoreConfirm')}
        okButtonProps={{ loading: restorePending }}
        cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
        onOk={async () => {
          try {
            await onRestore();
            setRestoreOpen(false);
          } catch {
            /* parent toast */
          }
        }}
        destroyOnHidden
      >
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          {t('tenants.detail.settings.danger.restoreModalBody')}
        </Typography.Paragraph>
      </Modal>

      {showDevelopmentHardDelete ? (
        <Modal
          title={t('tenants.detail.settings.danger.developmentHardDeleteButton')}
          open={developmentHardDeleteOpen}
          onCancel={closeDevelopmentHardDelete}
          okText={t('tenants.detail.settings.danger.developmentHardDeleteButton')}
          okButtonProps={{ danger: true, loading: developmentHardDeletePending }}
          cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
          onOk={async () => {
            try {
              await onDevelopmentHardDelete?.();
              closeDevelopmentHardDelete();
            } catch {
              /* parent toast */
            }
          }}
          destroyOnHidden
        >
          <Alert
            type="warning"
            showIcon
            title={t('tenants.detail.settings.danger.developmentHardDeleteHint')}
          />
        </Modal>
      ) : null}
    </Card>
  );
}
