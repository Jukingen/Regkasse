'use client';

import { Form, Input, Select, Switch } from 'antd';
import type { FormInstance } from 'antd/es/form';
import type { ReactNode } from 'react';

import type { CreateUserFormValues } from './types';

export type CreateUserNormalFormProps = {
  form: FormInstance<CreateUserFormValues>;
  onFinish: (values: CreateUserFormValues) => void | Promise<void>;
  roleOptions: { value: string; label: string }[];
  tenantField: ReactNode;
  showOwnerToggle: boolean;
  t: (key: string) => string;
};

export function CreateUserNormalForm({
  form,
  onFinish,
  roleOptions,
  tenantField,
  showOwnerToggle,
  t,
}: CreateUserNormalFormProps) {
  return (
    <Form form={form} layout="vertical" onFinish={onFinish}>
      <Form.Item
        name="email"
        label={t('users.create.email')}
        rules={[
          { required: true, message: t('users.create.emailRequired') },
          { type: 'email', message: t('users.create.emailInvalid') },
        ]}
      >
        <Input type="email" placeholder="benutzer@firma.at" />
      </Form.Item>

      <Form.Item name="firstName" label={t('users.create.firstName')}>
        <Input placeholder="Max" maxLength={50} />
      </Form.Item>

      <Form.Item name="lastName" label={t('users.create.lastName')}>
        <Input placeholder="Mustermann" maxLength={50} />
      </Form.Item>

      <Form.Item
        name="role"
        label={t('users.create.role')}
        rules={[{ required: true, message: t('users.create.roleRequired') }]}
      >
        <Select options={roleOptions} />
      </Form.Item>

      {tenantField}

      {showOwnerToggle ? (
        <Form.Item name="isOwner" label={t('users.create.isOwner')} valuePropName="checked">
          <Switch />
        </Form.Item>
      ) : null}
    </Form>
  );
}
