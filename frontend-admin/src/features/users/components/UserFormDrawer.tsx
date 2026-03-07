'use client';

/**
 * Create/Edit user form in a Drawer (i18n-ready copy).
 */
import React, { useEffect } from 'react';
import { Drawer, Form, Input, Select, Button, Space } from 'antd';
import type { UserInfo } from '@/api/generated/model';
import type { CreateUserRequest, UpdateUserRequest } from '@/api/generated/model';
import { usersCopy } from '../constants/copy';

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
            <Form.Item name="userName" label={usersCopy.userName} rules={[{ required: true }]}>
              <Input />
            </Form.Item>
            <Form.Item name="password" label={usersCopy.password} rules={[{ required: true, min: 6 }]}>
              <Input.Password />
            </Form.Item>
          </>
        )}
        <Form.Item name="firstName" label={usersCopy.firstName} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="lastName" label={usersCopy.lastName} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="email" label={usersCopy.email}>
          <Input type="email" />
        </Form.Item>
        <Form.Item name="employeeNumber" label={usersCopy.employeeNumber}>
          <Input />
        </Form.Item>
        <Form.Item name="role" label={usersCopy.role} rules={[{ required: true }]}>
          <Select options={roleOptions} />
        </Form.Item>
        <Form.Item name="taxNumber" label={usersCopy.taxNumber}>
          <Input />
        </Form.Item>
        <Form.Item name="notes" label={usersCopy.notes}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Drawer>
  );
}
