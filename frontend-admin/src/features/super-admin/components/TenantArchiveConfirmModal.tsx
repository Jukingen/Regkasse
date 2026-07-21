'use client';

import { useMutation } from '@tanstack/react-query';
import { Alert, Modal, Typography } from 'antd';
import React from 'react';

import { softDeleteAdminTenant } from '@/features/super-admin/api/adminTenants';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export type TenantArchiveConfirmModalProps = {
  open: boolean;
  tenantId: string;
  tenantName: string;
  onClose: () => void;
  onSuccess: () => void;
};

export function TenantArchiveConfirmModal({
  open,
  tenantId,
  tenantName,
  onClose,
  onSuccess,
}: TenantArchiveConfirmModalProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();

  const archiveMutation = useMutation({
    mutationFn: () => softDeleteAdminTenant(tenantId),
    onSuccess: () => {
      message.success(t('tenants.messages.archived'));
      onClose();
      onSuccess();
    },
    onError: () => {
      message.error(t('tenants.messages.deleteFailed'));
    },
  });

  const handleClose = () => {
    if (archiveMutation.isPending) return;
    onClose();
  };

  return (
    <Modal
      title={t('tenants.archiveConfirmModal.title')}
      open={open}
      onCancel={handleClose}
      okText={t('tenants.archiveConfirmModal.confirm')}
      okButtonProps={{ danger: true, loading: archiveMutation.isPending }}
      cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
      onOk={() => archiveMutation.mutateAsync()}
      destroyOnHidden
    >
      <Alert
        type="info"
        showIcon
        title={t('tenants.archiveConfirmModal.alertTitle')}
        description={t('tenants.archiveConfirmModal.alertBody')}
        style={{ marginBottom: 16 }}
      />
      <Typography.Text type="secondary">
        {t('tenants.archiveConfirmModal.tenantLine', { name: tenantName })}
      </Typography.Text>
    </Modal>
  );
}
