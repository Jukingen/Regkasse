'use client';

import { Alert, Form, Select, Typography } from 'antd';
import type { FormInstance } from 'antd/es/form';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { TenantSelector } from '@/features/users/components/TenantSelector';

import type { CreateUserQuickFormValues } from './types';

export type CreateUserQuickFormProps = {
  form: FormInstance<CreateUserQuickFormValues>;
  onFinish: (values: CreateUserQuickFormValues) => void | Promise<void>;
  showTenantPicker: boolean;
  canDeferTenantAssignment: boolean;
  tenantRows: AdminTenantListItem[];
  tenantsLoading: boolean;
  roleOptions: { value: string; label: string }[];
  suggestedUsername: string | undefined;
  usernamePatternFallback: string;
  usernameAlternates: string | null;
  emailPreview: string;
  watchedTenantId: string | undefined;
  t: (key: string, options?: Record<string, string | number>) => string;
};

export function CreateUserQuickForm({
  form,
  onFinish,
  showTenantPicker,
  canDeferTenantAssignment,
  tenantRows,
  tenantsLoading,
  roleOptions,
  suggestedUsername,
  usernamePatternFallback,
  usernameAlternates,
  emailPreview,
  watchedTenantId,
  t,
}: CreateUserQuickFormProps) {
  return (
    <Form form={form} layout="vertical" onFinish={onFinish}>
      {showTenantPicker ? (
        <Form.Item
          name="tenantId"
          label={t('users.create.tenant')}
          rules={
            canDeferTenantAssignment
              ? []
              : [{ required: true, message: t('users.create.tenantRequired') }]
          }
        >
          <TenantSelector
            tenants={tenantRows}
            loading={tenantsLoading}
            placeholder={
              canDeferTenantAssignment
                ? t('tenants.users.quick.tenantPlaceholderOptional')
                : t('users.create.tenantPlaceholder')
            }
          />
        </Form.Item>
      ) : null}

      <Form.Item
        name="role"
        label={t('tenants.users.quick.role')}
        rules={[{ required: true, message: t('users.create.roleRequired') }]}
      >
        <Select options={roleOptions} />
      </Form.Item>

      <div
        style={{
          marginTop: 16,
          padding: 12,
          background: '#fafafa',
          borderRadius: 8,
          border: '1px solid #f0f0f0',
        }}
      >
        <Typography.Paragraph style={{ marginBottom: 8 }}>
          <Typography.Text strong>{t('tenants.users.quick.preview.usernameLabel')}</Typography.Text>{' '}
          <Typography.Text code>{suggestedUsername ?? usernamePatternFallback}</Typography.Text>
        </Typography.Paragraph>
        {usernameAlternates ? (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
            {t('tenants.users.quick.preview.usernameAvailable', { list: usernameAlternates })}
          </Typography.Paragraph>
        ) : null}
        <Typography.Paragraph style={{ marginBottom: 8 }}>
          <Typography.Text strong>{t('tenants.users.quick.preview.emailLabel')}</Typography.Text>{' '}
          {emailPreview}
        </Typography.Paragraph>
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          <Typography.Text strong>{t('tenants.users.quick.preview.passwordLabel')}</Typography.Text>{' '}
          {t('tenants.users.quick.autoPassword')}
        </Typography.Paragraph>
      </div>

      <Alert
        type="info"
        showIcon
        style={{ marginTop: 16 }}
        title={t('tenants.users.quick.autoTitle')}
        description={t('tenants.users.quick.autoForceChange')}
      />
      {showTenantPicker && canDeferTenantAssignment && !watchedTenantId ? (
        <Alert
          type="warning"
          showIcon
          title={t('tenants.users.quick.assignmentRequiredAfterCreate')}
          style={{ marginTop: 16 }}
        />
      ) : null}
    </Form>
  );
}
