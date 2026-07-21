'use client';

import { CopyOutlined } from '@ant-design/icons';
import { Alert, Button, Modal, Space, Typography } from 'antd';
import { useEffect, useState } from 'react';

import {
  type TenantUser,
  type TenantUserPasswordResetResult,
  resetTenantUserPassword,
} from '@/features/super-admin/api/tenantUsers';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type ResetPasswordModalProps = {
  open: boolean;
  tenantId: string;
  user: TenantUser | null;
  onClose: () => void;
  onCompleted?: () => void;
};

export function ResetPasswordModal({
  open,
  tenantId,
  user,
  onClose,
  onCompleted,
}: ResetPasswordModalProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const [confirming, setConfirming] = useState(false);
  const [result, setResult] = useState<TenantUserPasswordResetResult | null>(null);
  const [password, setPassword] = useState('');

  useEffect(() => {
    if (open) {
      setResult(null);
      setPassword('');
    }
  }, [open]);

  useEffect(() => {
    if (open && result?.generatedPassword) {
      setPassword(result.generatedPassword);
      return;
    }

    if (!result?.generatedPassword) {
      setPassword('');
    }
  }, [open, result]);

  const handleConfirm = async () => {
    if (!user) return;
    setConfirming(true);
    try {
      const res = await resetTenantUserPassword(tenantId, user.userId);
      setResult(res);
      onCompleted?.();
      message.success(t('tenants.users.messages.passwordReset'));
    } catch {
      message.error(t('tenants.users.messages.passwordResetFailed'));
    } finally {
      setConfirming(false);
    }
  };

  const copyPassword = async () => {
    if (!password) {
      message.error(t('users.password.noPasswordToCopy'));
      return;
    }

    const copied = await copyTextToClipboard(password);
    if (copied) {
      message.success(t('tenants.provisioning.copySuccess'));
    } else {
      message.error(t('tenants.provisioning.copyFailed'));
    }
  };

  const handleClose = () => {
    setResult(null);
    onClose();
  };

  return (
    <Modal
      title={t('tenants.users.resetPassword.title')}
      open={open}
      onCancel={handleClose}
      destroyOnHidden
      footer={
        result
          ? [
              <Button key="ok" type="primary" onClick={handleClose}>
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
                loading={confirming}
                onClick={() => void handleConfirm()}
              >
                {t('tenants.users.resetPassword.confirm')}
              </Button>,
            ]
      }
    >
      {result ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Text type="secondary">{result.deliveryNote}</Typography.Text>
          {result.forcePasswordChangeOnNextLogin ? (
            <Alert type="info" showIcon title={t('tenants.users.resetPassword.forceChangeHint')} />
          ) : null}
          {result.generatedPassword ? (
            <Alert
              type="warning"
              showIcon
              title={t('users.create.generatedPasswordInfo')}
              description={
                <Space>
                  <Typography.Text code>{password}</Typography.Text>
                  <Button size="small" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                    {t('tenants.provisioning.copyPassword')}
                  </Button>
                </Space>
              }
            />
          ) : null}
        </Space>
      ) : user ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Text>
            {t('tenants.users.resetPassword.body', {
              name: user.name,
              email: user.email,
            })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {t('tenants.users.resetPassword.smtpOff')}
          </Typography.Text>
        </Space>
      ) : null}
    </Modal>
  );
}
