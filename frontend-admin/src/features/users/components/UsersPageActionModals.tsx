'use client';

import { Alert, Form, Input, Modal, Select } from 'antd';
import type { Rule } from 'antd/es/form';
import React, { useEffect, useMemo } from 'react';

import type { UserInfo } from '@/features/users/api/usersGateway';
import { RolePresetPreviewCard } from '@/features/users/components/RolePresetPreviewCard';
import {
  ROLE_PRESETS,
  findRolePresetById,
} from '@/features/users/constants/rolePresets';
import { PASSWORD_MIN_LENGTH } from '@/features/users/constants/validation';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { useI18n } from '@/i18n';

function fullName(record: UserInfo): string {
  const first = record.firstName ?? '';
  const last = record.lastName ?? '';
  const name = `${first} ${last}`.trim();
  return name || record.userName || record.id || '—';
}

type DeactivateUserModalProps = {
  user: UserInfo;
  onCancel: () => void;
  onConfirm: (reason: string) => void;
  confirmLoading?: boolean;
  reasonRules: Rule[];
};

export function DeactivateUserModal({
  user,
  onCancel,
  onConfirm,
  confirmLoading,
  reasonRules,
}: DeactivateUserModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ reason: string }>();

  const handleOk = () => {
    void form.validateFields().then(
      (values) => onConfirm(values.reason),
      () => {
        /* validation shown on form */
      }
    );
  };

  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  return (
    <Modal
      title={t('users.modals.deactivate.title')}
      open
      onOk={handleOk}
      onCancel={handleCancel}
      okText={t('users.modals.deactivate.confirm')}
      okButtonProps={{ danger: true }}
      confirmLoading={confirmLoading}
    >
      <p style={{ marginBottom: 16 }}>
        <strong>{fullName(user)}</strong> ({user.email ?? user.userName}){' '}
        {t('users.modals.deactivate.confirmSuffix')}
      </p>
      <Form form={form} layout="vertical">
        <Form.Item
          name="reason"
          label={t('users.modals.deactivate.reasonLabel')}
          rules={reasonRules}
        >
          <Input.TextArea
            rows={3}
            placeholder={t('users.modals.deactivate.reasonPlaceholder')}
            maxLength={500}
            showCount
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}

type ResetPasswordUserModalProps = {
  user: UserInfo;
  onCancel: () => void;
  onConfirm: (newPassword: string) => void;
  confirmLoading?: boolean;
  passwordRules: Rule[];
  validationError?: string | null;
  onClearValidationError?: () => void;
};

export function ResetPasswordUserModal({
  user,
  onCancel,
  onConfirm,
  confirmLoading,
  passwordRules,
  validationError,
  onClearValidationError,
}: ResetPasswordUserModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ newPassword: string }>();
  const passwordMin = PASSWORD_MIN_LENGTH;

  useEffect(() => {
    form.resetFields();
    onClearValidationError?.();
  }, [user.id, form, onClearValidationError]);

  useEffect(() => {
    if (validationError) {
      form.setFields([{ name: 'newPassword', errors: [validationError] }]);
    }
  }, [validationError, form]);

  const handleOk = () => {
    void form
      .validateFields()
      .then((values) => onConfirm(values.newPassword))
      .catch(() => {
        /* validation shown on form */
      });
  };

  const handleCancel = () => {
    form.resetFields();
    onClearValidationError?.();
    onCancel();
  };

  return (
    <Modal
      title={t('users.modals.resetPassword.title')}
      open
      onOk={handleOk}
      onCancel={handleCancel}
      okText={t('common.buttons.save')}
      confirmLoading={confirmLoading}
    >
      <p style={{ marginBottom: 8 }}>
        <strong>{fullName(user)}</strong> ({user.userName})
      </p>
      <Alert
        type="info"
        title={t('users.modals.resetPassword.securityNote', { min: passwordMin })}
        showIcon
        style={{ marginBottom: 16 }}
      />
      {validationError ? (
        <Alert type="error" title={validationError} showIcon style={{ marginBottom: 16 }} />
      ) : null}
      <Form form={form} layout="vertical">
        <Form.Item
          name="newPassword"
          label={t('users.modals.resetPassword.newPasswordLabel', { min: passwordMin })}
          rules={passwordRules}
        >
          <Input.Password
            placeholder={t('common.auth.passwordMaskedPlaceholder')}
            autoComplete="new-password"
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}

type CreateRoleModalProps = {
  onCancel: () => void;
  onConfirm: (payload: {
    name: string;
    inheritFromRole?: string;
    presetId?: string;
  }) => void;
  confirmLoading?: boolean;
  roleNameRules: Rule[];
  inheritRoleOptions?: { value: string; label: string }[];
};

export function CreateRoleModal({
  onCancel,
  onConfirm,
  confirmLoading,
  roleNameRules,
  inheritRoleOptions = [],
}: CreateRoleModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ name: string; inheritFromRole?: string; presetId?: string }>();
  const selectedPresetId = Form.useWatch('presetId', form);
  const selectedInherit = Form.useWatch('inheritFromRole', form);
  const { data: catalog = [] } = usePermissionsCatalog();

  const selectableInheritOptions = useMemo(
    () => inheritRoleOptions.filter((option) => option.value !== 'SuperAdmin'),
    [inheritRoleOptions]
  );

  const presetOptions = useMemo(
    () =>
      ROLE_PRESETS.map((p) => ({
        value: p.id,
        label: p.label,
      })),
    []
  );

  const selectedPreset = useMemo(
    () => findRolePresetById(selectedPresetId),
    [selectedPresetId]
  );

  const catalogKeySet = useMemo(
    () => (catalog.length ? new Set(catalog.map((item) => item.key)) : undefined),
    [catalog]
  );

  const handleOk = () => {
    void form
      .validateFields()
      .then((values) =>
        onConfirm({
          name: values.name.trim(),
          inheritFromRole: values.inheritFromRole?.trim() || undefined,
          presetId: values.presetId?.trim() || undefined,
        })
      )
      .catch(() => {
        /* validation shown on form */
      });
  };

  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  return (
    <Modal
      title={t('users.page.createRole')}
      open
      destroyOnHidden
      onOk={handleOk}
      onCancel={handleCancel}
      afterClose={() => form.resetFields()}
      okText={t('common.buttons.save')}
      confirmLoading={confirmLoading}
    >
      <Form form={form} layout="vertical">
        <Form.Item name="name" label={t('users.createRole.nameLabel')} rules={roleNameRules}>
          <Input
            placeholder={t('users.createRole.namePlaceholder')}
            maxLength={50}
            showCount
            autoComplete="off"
          />
        </Form.Item>
        {selectableInheritOptions.length > 0 ? (
          <Form.Item
            name="inheritFromRole"
            label={t('users.createRole.inheritFromRole')}
            extra={t('users.createRole.inheritFromRoleHelp')}
          >
            <Select
              allowClear
              placeholder={t('users.createRole.inheritFromRolePlaceholder')}
              options={selectableInheritOptions}
              disabled={Boolean(selectedPresetId)}
              onChange={(value) => {
                if (value) form.setFieldValue('presetId', undefined);
              }}
            />
          </Form.Item>
        ) : null}
        <Form.Item
          name="presetId"
          label={t('users.createRole.presetLabel')}
          extra={t('users.createRole.presetHelp')}
        >
          <Select
            allowClear
            placeholder={t('users.roleDrawer.presetPlaceholder')}
            options={presetOptions}
            disabled={Boolean(selectedInherit)}
            onChange={(value) => {
              if (value) form.setFieldValue('inheritFromRole', undefined);
            }}
            optionRender={(option) => {
              const preset = findRolePresetById(String(option.value));
              return (
                <div>
                  <div>{option.label}</div>
                  {preset ? (
                    <div style={{ fontSize: 11, color: 'rgba(0,0,0,0.45)' }}>{preset.description}</div>
                  ) : null}
                </div>
              );
            }}
          />
        </Form.Item>
        {selectedPreset ? (
          <RolePresetPreviewCard preset={selectedPreset} catalogKeys={catalogKeySet} />
        ) : null}
      </Form>
    </Modal>
  );
}
