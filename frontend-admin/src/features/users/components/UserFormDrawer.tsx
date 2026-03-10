'use client';

/**
 * Create/Edit user form in a Drawer – merkezi validasyon, backend contract uyumlu.
 * Edit: form is filled only from detail API response (user prop). Never use list row data.
 * When open in edit mode: show loading until user is loaded, then setFieldsValue(user); on close parent clears selectedUserId.
 */
import React, { useEffect, useMemo } from 'react';
import { Drawer, Form, Input, Select, Button, Space, Spin, Alert } from 'antd';
import type { UserInfo } from '@/api/generated/model';
import type { CreateUserRequest, UpdateUserRequest } from '@/api/generated/model';
import { usersCopy } from '../constants/copy';
import { createUsersFormRules, NAME_MAX_LENGTH, EMAIL_MAX_LENGTH, SHORT_FIELD_MAX_LENGTH, NOTES_MAX_LENGTH } from '../constants/validation';

type Mode = 'create' | 'edit';

type Props = {
  open: boolean;
  onClose: () => void;
  mode: Mode;
  /** For edit: full user from GET /api/UserManagement/{id} (includes Notes). Undefined while loading. */
  user?: UserInfo | null;
  /** True while fetching user in edit mode; form shows loading until user is set. */
  initialLoading?: boolean;
  /** When edit mode and detail fetch failed; show error and retry. */
  fetchError?: unknown;
  /** Retry callback for detail fetch (edit mode). */
  onRetryFetch?: () => void;
  roleOptions: { value: string; label: string }[];
  onSubmit: (values: CreateUserRequest | UpdateUserRequest) => void;
  loading?: boolean;
};

const formRulesContext = {
  requiredMessage: usersCopy.validationRequired,
  emailInvalidMessage: usersCopy.validationEmail,
  passwordMinMessage: usersCopy.validationPasswordMin,
  passwordPolicyMessage: usersCopy.validationPasswordPolicy,
  maxLengthMessage: usersCopy.validationMaxLength,
  reasonRequiredMessage: usersCopy.reasonRequiredMessage,
  roleNameRequiredMessage: usersCopy.roleNameRequired,
};

/**
 * Map UserInfo to form field values. Keys must match Form.Item name props exactly:
 * firstName, lastName, email, employeeNumber, role, taxNumber, notes.
 * Backend returns role as string (e.g. "SuperAdmin", "Manager"); form select uses same string.
 */
function userToFormValues(u: UserInfo | Record<string, unknown>): Record<string, string> {
  const get = (key: string, altKeys: string[] = []) => {
    const obj = u as Record<string, unknown>;
    if (obj[key] != null && obj[key] !== '') return String(obj[key]);
    for (const k of altKeys) {
      if (obj[k] != null && obj[k] !== '') return String(obj[k]);
    }
    return '';
  };
  return {
    firstName: get('firstName', ['FirstName']),
    lastName: get('lastName', ['LastName']),
    email: get('email', ['Email']),
    employeeNumber: get('employeeNumber', ['EmployeeNumber', 'employeeNo']),
    role: get('role', ['Role', 'roleName']),
    taxNumber: get('taxNumber', ['TaxNumber', 'taxNo']),
    notes: get('notes', ['Notes', 'comment', 'description']),
  };
}

export function UserFormDrawer({
  open,
  onClose,
  mode,
  user,
  initialLoading = false,
  fetchError = null,
  onRetryFetch,
  roleOptions,
  onSubmit,
  loading = false,
}: Props) {
  const [form] = Form.useForm();
  const rules = useMemo(() => createUsersFormRules(formRulesContext), []);

  useEffect(() => {
    if (!open) return;
    if (mode === 'create') {
      form.resetFields();
      return;
    }
    // Edit + user still loading: Form is not mounted yet; resetFields would disconnect useForm.
    if (mode === 'edit' && user == null) return;
  }, [open, mode, user, form]);

  const formValues = mode === 'edit' && user != null ? userToFormValues(user) : null;
  useEffect(() => {
    if (!open || mode !== 'edit' || user == null) return;
    const values = userToFormValues(user);
    const id = setTimeout(() => {
      form.resetFields();
      form.setFieldsValue(values);
    }, 0);
    return () => clearTimeout(id);
  }, [open, mode, user, form]);

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      const raw = values as Record<string, unknown>;
      const normalized = {
        ...raw,
        employeeNumber: typeof raw.employeeNumber === 'string' ? raw.employeeNumber.trim() : '',
      };
      onSubmit(normalized as CreateUserRequest & UpdateUserRequest);
    });
  };

  const title = mode === 'create' ? usersCopy.createUser : usersCopy.editUser;
  const saveDisabled = mode === 'edit' && (initialLoading || !!fetchError);
  // Edit: never render form until user detail is loaded (avoids empty form + initialValues-only hydration).
  const showForm = mode === 'create' || (mode === 'edit' && user != null);

  return (
    <Drawer
      title={title}
      placement="right"
      width={480}
      open={open}
      onClose={onClose}
      destroyOnHidden
      footer={
        <Space>
          <Button onClick={onClose}>{usersCopy.cancel}</Button>
          <Button type="primary" onClick={handleSubmit} loading={loading} disabled={saveDisabled}>
            {usersCopy.save}
          </Button>
        </Space>
      }
    >
      {mode === 'edit' && (initialLoading || user == null) ? (
        // Spin tip only works in nest pattern (child required); avoid useForm-disconnected phase without Form mounted.
        <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
          <Spin spinning tip="Laden…">
            <div style={{ minHeight: 80 }} />
          </Spin>
        </div>
      ) : mode === 'edit' && fetchError ? (
        <Alert
          type="error"
          message={usersCopy.errorLoadUser}
          description={((fetchError as { response?: { data?: { message?: string } }; message?: string })?.response?.data?.message ?? (fetchError as { message?: string })?.message) ?? String(fetchError)}
          action={onRetryFetch && <Button size="small" onClick={onRetryFetch}>Erneut versuchen</Button>}
          showIcon
        />
      ) : showForm ? (
        <Form
          key={mode === 'edit' && user ? `edit-${user.id ?? 'user'}` : 'create'}
          form={form}
          layout="vertical"
          preserve={false}
          initialValues={formValues ?? undefined}
        >
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
      ) : null}
    </Drawer>
  );
}
