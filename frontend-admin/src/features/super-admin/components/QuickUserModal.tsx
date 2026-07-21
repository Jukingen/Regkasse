'use client';

import { ThunderboltOutlined } from '@ant-design/icons';
import { Alert, Form, Modal, Select, Typography } from 'antd';
import React, { useEffect, useMemo } from 'react';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { QUICK_USER_ROLES } from '@/features/super-admin/api/quickUser';
import { TenantSelector } from '@/features/users/components/TenantSelector';
import { useI18n } from '@/i18n';

export type QuickUserFormValues = {
  role: string;
  tenantId?: string;
};

export type QuickUserModalProps = {
  open: boolean;
  confirmLoading?: boolean;
  /** Fixed mandant (tenant detail); hides selector when set. */
  tenantId?: string;
  tenantSlug?: string;
  tenantName?: string;
  /** Mandant picker on unified users page. */
  tenantRows?: AdminTenantListItem[];
  tenantsLoading?: boolean;
  variant?: 'tenantDetail' | 'usersPage';
  onClose: () => void;
  onSubmit: (values: QuickUserFormValues) => void;
};

export function QuickUserModal(props: QuickUserModalProps) {
  if (!props.open) {
    return null;
  }
  return <QuickUserModalContent {...props} />;
}

function QuickUserModalContent({
  open,
  confirmLoading,
  tenantId: fixedTenantId,
  tenantSlug: fixedTenantSlug,
  tenantName: fixedTenantName,
  tenantRows = [],
  tenantsLoading = false,
  variant = 'tenantDetail',
  onClose,
  onSubmit,
}: QuickUserModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<QuickUserFormValues>();

  const showTenantSelector = variant === 'usersPage' && !fixedTenantId && tenantRows.length > 0;

  const roleOptions = useMemo(
    () =>
      QUICK_USER_ROLES.map((role) => ({
        value: role,
        label: t(`users.create.roleOptions.${role}.label`, { defaultValue: role }),
      })),
    [t]
  );

  const tenantById = useMemo(() => new Map(tenantRows.map((row) => [row.id, row])), [tenantRows]);

  useEffect(() => {
    if (!open) {
      form.resetFields();
      return;
    }
    form.setFieldsValue({
      role: 'Manager',
      ...(fixedTenantId ? { tenantId: fixedTenantId } : {}),
    });
  }, [open, form, fixedTenantId]);

  const watchedRole = Form.useWatch('role', form) ?? 'Manager';
  const watchedTenantId = Form.useWatch('tenantId', form) ?? fixedTenantId;
  const previewTenant = watchedTenantId ? tenantById.get(watchedTenantId) : undefined;
  const previewSlug = previewTenant?.slug ?? fixedTenantSlug ?? 'platform';

  const infoEmailExample = t('tenants.users.quick.emailPreview', {
    role: watchedRole.toLowerCase(),
    random: 'a3f9k2',
    slug: previewSlug,
  });

  const handleFinish = (values: QuickUserFormValues) => {
    onSubmit({
      role: values.role,
      tenantId: fixedTenantId ?? values.tenantId,
    });
  };

  return (
    <Modal
      title={
        <span>
          <ThunderboltOutlined style={{ marginRight: 8 }} />
          {t('tenants.users.quick.title')}
        </span>
      }
      open={open}
      onCancel={onClose}
      onOk={() => form.submit()}
      okText={t('tenants.users.quick.generate')}
      cancelText={t('common.buttons.cancel')}
      confirmLoading={confirmLoading}
      forceRender
    >
      <Form form={form} layout="vertical" onFinish={handleFinish}>
        {showTenantSelector ? (
          <Form.Item name="tenantId" label={t('users.create.tenant')}>
            <TenantSelector
              tenants={tenantRows}
              loading={tenantsLoading}
              placeholder={t('tenants.users.quick.tenantPlaceholderOptional')}
            />
          </Form.Item>
        ) : fixedTenantName && fixedTenantSlug ? (
          <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
            {fixedTenantName} ({fixedTenantSlug})
          </Typography.Paragraph>
        ) : null}
        <Form.Item
          name="role"
          label={t('tenants.users.quick.role')}
          rules={[{ required: true, message: t('users.create.roleRequired') }]}
        >
          <Select options={roleOptions} />
        </Form.Item>
        <Alert
          type="info"
          showIcon
          title={t('tenants.users.quick.autoTitle')}
          description={
            <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
              <li>{infoEmailExample}</li>
              <li>{t('tenants.users.quick.autoPassword')}</li>
              <li>{t('tenants.users.quick.autoForceChange')}</li>
            </ul>
          }
        />
        {showTenantSelector && !watchedTenantId ? (
          <Alert
            type="warning"
            showIcon
            title={t('tenants.users.quick.assignmentRequiredAfterCreate')}
            style={{ marginTop: 16 }}
          />
        ) : null}
      </Form>
    </Modal>
  );
}
