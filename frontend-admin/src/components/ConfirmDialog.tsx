'use client';

import { Button, Modal, Space } from 'antd';

import { useI18n } from '@/i18n';

export type ConfirmDialogType = 'danger' | 'warning' | 'info';

export type ConfirmDialogProps = {
  open: boolean;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  type?: ConfirmDialogType;
  onConfirm: () => void;
  onCancel: () => void;
  loading?: boolean;
};

export function ConfirmDialog({
  open,
  title,
  message,
  confirmText,
  cancelText,
  type = 'warning',
  onConfirm,
  onCancel,
  loading = false,
}: ConfirmDialogProps) {
  const { t } = useI18n();

  const resolvedConfirmText = confirmText ?? t('common.buttons.confirm');
  const resolvedCancelText = cancelText ?? t('common.buttons.cancel');

  return (
    <Modal
      open={open}
      title={title}
      onCancel={loading ? undefined : onCancel}
      destroyOnHidden
      maskClosable={!loading}
      keyboard={!loading}
      footer={
        <Space>
          <Button onClick={onCancel} disabled={loading}>
            {resolvedCancelText}
          </Button>
          <Button type="primary" danger={type === 'danger'} onClick={onConfirm} loading={loading}>
            {resolvedConfirmText}
          </Button>
        </Space>
      }
    >
      <div style={{ margin: '16px 0' }}>{message}</div>
    </Modal>
  );
}
