'use client';

import { useEffect, useState } from 'react';
import { Alert, Button, Modal, Space, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import type { UserInfo } from '@/features/users/api/usersGateway';
import { useResetUserPassword } from '@/features/users/hooks/useResetPassword';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type ResetPasswordModalProps = {
  open: boolean;
  user: UserInfo | null;
  onClose: () => void;
  onSuccess?: () => void;
};

function displayName(user: UserInfo): string {
  const first = user.firstName ?? '';
  const last = user.lastName ?? '';
  const name = `${first} ${last}`.trim();
  return name || user.userName || user.email || '—';
}

export function ResetPasswordModal({ open, user, onClose, onSuccess }: ResetPasswordModalProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const mutation = useResetUserPassword();
  const [generatedPassword, setGeneratedPassword] = useState('');

  useEffect(() => {
    if (open) {
      setGeneratedPassword('');
      mutation.reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when modal opens
  }, [open, user?.id]);

  const handleReset = async () => {
    if (!user?.id) return;
    try {
      const result = await mutation.mutateAsync(user.id);
      setGeneratedPassword(result.generatedPassword);
      message.success(t('users.messages.successResetPassword'));
      onSuccess?.();
    } catch {
      message.error(t('users.messages.errorResetPassword'));
    }
  };

  const copyPassword = async () => {
    if (!generatedPassword) return;
    const copied = await copyTextToClipboard(generatedPassword);
    if (copied) {
      message.success(t('tenants.provisioning.copySuccess'));
    } else {
      message.error(t('tenants.provisioning.copyFailed'));
    }
  };

  const handleClose = () => {
    setGeneratedPassword('');
    onClose();
  };

  return (
    <Modal
      title={t('users.modals.resetPassword.generated.title')}
      open={open}
      onCancel={handleClose}
      destroyOnHidden
      footer={
        generatedPassword
          ? [
              <Button key="close" type="primary" onClick={handleClose}>
                {t('common.ok', { defaultValue: 'OK' })}
              </Button>,
            ]
          : [
              <Button key="cancel" onClick={handleClose}>
                {t('common.cancel', { defaultValue: 'Abbrechen' })}
              </Button>,
              <Button
                key="reset"
                type="primary"
                danger
                loading={mutation.isPending}
                onClick={() => void handleReset()}
              >
                {t('users.modals.resetPassword.generated.confirm')}
              </Button>,
            ]
      }
    >
      {generatedPassword ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert type="success" showIcon title={t('users.modals.resetPassword.generated.resultTitle')} />
          <Alert type="info" showIcon title={t('users.modals.resetPassword.generated.forceChangeHint')} />
          <div style={{ background: '#f8fafc', padding: 16, borderRadius: 8 }}>
            <Typography.Text strong>{t('users.modals.resetPassword.generated.passwordLabel')}</Typography.Text>
            <Space style={{ marginTop: 8 }}>
              <Typography.Text code style={{ fontSize: 18 }}>
                {generatedPassword}
              </Typography.Text>
              <Button size="small" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                {t('tenants.provisioning.copyPassword')}
              </Button>
            </Space>
            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
              {t('users.modals.resetPassword.generated.copyHint')}
            </Typography.Paragraph>
          </div>
        </Space>
      ) : user ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert
            type="warning"
            showIcon
            title={t('users.modals.resetPassword.generated.confirmTitle', { name: displayName(user) })}
            description={t('users.modals.resetPassword.generated.body')}
          />
        </Space>
      ) : null}
    </Modal>
  );
}
