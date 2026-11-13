'use client';

import { LockOutlined } from '@ant-design/icons';
import { Button, Card, Form, Input } from 'antd';
import { useState } from 'react';

import { allPasswordRequirementsMet } from '@/features/auth/lib/passwordRequirements';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { customInstance } from '@/lib/axios';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

type ChangeMyPasswordValues = {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
};

export type ChangeMyPasswordFormProps = {
  /** When false, renders form only (no Card wrapper). */
  bordered?: boolean;
};

export function ChangeMyPasswordForm({ bordered = true }: ChangeMyPasswordFormProps) {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const [form] = Form.useForm<ChangeMyPasswordValues>();
  const [loading, setLoading] = useState(false);

  const validateNewPassword = (_: unknown, value: string) => {
    if (!value) {
      return Promise.reject(new Error(t('settings.changePassword.newPasswordRequired')));
    }
    if (!allPasswordRequirementsMet(value)) {
      return Promise.reject(new Error(t('settings.changePassword.requirementsNotMet')));
    }
    return Promise.resolve();
  };

  const onFinish = async (values: ChangeMyPasswordValues) => {
    if (values.newPassword !== values.confirmPassword) {
      form.setFields([
        { name: 'confirmPassword', errors: [t('settings.changePassword.confirmMismatch')] },
      ]);
      return;
    }
    setLoading(true);
    try {
      await customInstance<{ message?: string }>({
        url: '/api/UserManagement/me/password',
        method: 'PUT',
        data: { currentPassword: values.currentPassword, newPassword: values.newPassword },
      });
      message.success(t('settings.changePassword.success'));
      form.resetFields();
    } catch (err: unknown) {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          fallbackKey: 'settings.changePassword.errorFallback',
          logContext: 'changeOwnPassword',
        })
      );
    } finally {
      setLoading(false);
    }
  };

  const formNode = (
    <Form form={form} layout="vertical" onFinish={onFinish} style={{ maxWidth: 400 }}>
      <Form.Item
        name="currentPassword"
        label={t('settings.changePassword.currentPassword')}
        rules={[{ required: true, message: t('settings.changePassword.currentPasswordRequired') }]}
      >
        <Input.Password
          placeholder={t('settings.changePassword.currentPasswordPlaceholder')}
          autoComplete="current-password"
        />
      </Form.Item>
      <Form.Item
        name="newPassword"
        label={t('settings.changePassword.newPassword')}
        rules={[
          { required: true, message: t('settings.changePassword.newPasswordRequired') },
          { validator: validateNewPassword },
        ]}
      >
        <Input.Password
          placeholder={t('settings.changePassword.newPasswordPlaceholder')}
          autoComplete="new-password"
        />
      </Form.Item>
      <Form.Item
        name="confirmPassword"
        label={t('settings.changePassword.confirmPassword')}
        dependencies={['newPassword']}
        rules={[
          { required: true, message: t('settings.changePassword.confirmRequired') },
          ({ getFieldValue }) => ({
            validator(_, value) {
              if (!value || getFieldValue('newPassword') === value) return Promise.resolve();
              return Promise.reject(new Error(t('settings.changePassword.confirmMismatch')));
            },
          }),
        ]}
      >
        <Input.Password
          placeholder={t('settings.changePassword.confirmPasswordPlaceholder')}
          autoComplete="new-password"
        />
      </Form.Item>
      <Form.Item>
        <Button type="primary" htmlType="submit" loading={loading} icon={<LockOutlined />}>
          {t('settings.changePassword.submit')}
        </Button>
      </Form.Item>
    </Form>
  );

  if (!bordered) {
    return formNode;
  }

  return <Card title={t('settings.changePassword.title')}>{formNode}</Card>;
}
