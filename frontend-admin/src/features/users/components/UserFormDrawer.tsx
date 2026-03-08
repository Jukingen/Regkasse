'use client';

/**
 * Create/Edit user form in a Drawer – merkezi validasyon, backend contract uyumlu.
 */
import React, { useEffect, useMemo } from 'react';
import { Drawer, Form, Input, Select, Button, Space } from 'antd';
import type { UserInfo } from '@/api/generated/model';
import type { CreateUserRequest, UpdateUserRequest } from '@/api/generated/model';
import { usersCopy } from '../constants/copy';
import { createUsersFormRules, NAME_MAX_LENGTH, EMAIL_MAX_LENGTH, SHORT_FIELD_MAX_LENGTH, NOTES_MAX_LENGTH } from '../constants/validation';

type Mode = 'create' | 'edit';

type Props = {
  open: boolean;
  onClose: () => void;
  mode: Mode;
  user?: UserInfo | null;
  roleOptions: { value: string; label: string }[];
  onSubmit: (values: CreateUserRequest | UpdateUserRequest) => void;
  loading?: boolean;
};

const formRulesContext = {
  requiredMessage: usersCopy.validationRequired,
  emailInvalidMessage: usersCopy.validationEmail,
  passwordMinMessage: usersCopy.validationPasswordMin,
  maxLengthMessage: usersCopy.validationMaxLength,
  reasonRequiredMessage: usersCopy.reasonRequiredMessage,
  roleNameRequiredMessage: usersCopy.roleNameRequired,
};

export function UserFormDrawer({
  open,
  onClose,
  mode,
  user,
  roleOptions,
  onSubmit,
  loading = false,
}: Props) {
  const [form] = Form.useForm();
  const rules = useMemo(() => createUsersFormRules(formRulesContext), []);

  useEffect(() => {
    if (!open) return;
    if (mode === 'edit' && user) {
      form.setFieldsValue({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        employeeNumber: user.employeeNumber,
        role: user.role,
        taxNumber: user.taxNumber,
        notes: user.notes,
      });
    } else {
      form.resetFields();
    }
  }, [open, mode, user, form]);

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      onSubmit(values as CreateUserRequest & UpdateUserRequest);
    });
  };

  const title = mode === 'create' ? usersCopy.createUser : usersCopy.editUser;

  return (
    <Drawer
      title={title}
      placement="right"
      width={480}
      open={open}
      onClose={onClose}
      destroyOnClose
      footer={
        <Space>
          <Button onClick={onClose}>{usersCopy.cancel}</Button>
          <Button type="primary" onClick={handleSubmit} loading={loading}>
            {usersCopy.save}
          </Button>
        </Space>
      }
    >
      <Form form={form} layout="vertical" preserve={false}>
        {mode === 'create' && (
          <>
            <Form.Item name="userName" label={usersCopy.userName} rules={rules.userName}>
              <Input maxLength={NAME_MAX_LENGTH} showCount placeholder={usersCopy.userName} />
            </Form.Item>
            <Form.Item name="password" label={usersCopy.password} rules={rules.password}>
              <Input.Password autoComplete="new-password" placeholder="••••••••" />
            </Form.Item>
          </>
        )}
        <Form.Item name="firstName" label={usersCopy.firstName} rules={rules.firstName}>
          <Input maxLength={NAME_MAX_LENGTH} />
        </Form.Item>
        <Form.Item name="lastName" label={usersCopy.lastName} rules={rules.lastName}>
          <Input maxLength={NAME_MAX_LENGTH} />
        </Form.Item>
        <Form.Item name="email" label={usersCopy.email} rules={rules.email}>
          <Input type="email" maxLength={EMAIL_MAX_LENGTH} />
        </Form.Item>
        <Form.Item name="employeeNumber" label={usersCopy.employeeNumber} rules={rules.employeeNumber}>
          <Input maxLength={SHORT_FIELD_MAX_LENGTH} />
        </Form.Item>
        <Form.Item name="role" label={usersCopy.role} rules={rules.role}>
          <Select options={roleOptions} placeholder={usersCopy.filterRole} />
        </Form.Item>
        <Form.Item name="taxNumber" label={usersCopy.taxNumber} rules={rules.taxNumber}>
          <Input maxLength={SHORT_FIELD_MAX_LENGTH} />
        </Form.Item>
        <Form.Item name="notes" label={usersCopy.notes} rules={rules.notes}>
          <Input.TextArea rows={2} maxLength={NOTES_MAX_LENGTH} showCount />
        </Form.Item>
      </Form>
    </Drawer>
  );
}
