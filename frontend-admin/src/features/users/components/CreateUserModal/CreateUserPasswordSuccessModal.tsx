'use client';

import { Alert, Button, Modal, Space } from 'antd';

import { CredentialCopyRow } from '@/features/super-admin/components/CredentialCopyRow';
import type { CreateUserResult } from '@/features/users/api/users';

export type CreateUserPasswordSuccessModalProps = {
  open: boolean;
  result: CreateUserResult | null;
  password: string;
  onClose: () => void;
  t: (key: string) => string;
};

export function CreateUserPasswordSuccessModal({
  open,
  result,
  password,
  onClose,
  t,
}: CreateUserPasswordSuccessModalProps) {
  return (
    <Modal
      title={t('users.create.success')}
      open={open}
      onCancel={onClose}
      closable
      mask={{ closable: true }}
      destroyOnHidden
      footer={[
        <Button key="close" type="primary" onClick={onClose}>
          {t('users.create.close')}
        </Button>,
      ]}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {result?.userName ? (
          <CredentialCopyRow
            label={t('tenants.users.quick.result.usernameLabel')}
            value={result.userName}
          />
        ) : null}
        {result?.email ? (
          <CredentialCopyRow
            label={t('tenants.users.quick.result.emailLabel')}
            value={result.email}
          />
        ) : null}
        <CredentialCopyRow label={t('users.create.password')} value={password} />
        <Alert
          type="warning"
          showIcon
          title={t('users.create.passwordWarningTitle')}
          description={t('users.create.generatedPasswordInfo')}
        />
      </Space>
    </Modal>
  );
}
