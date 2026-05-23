'use client';

/**
 * Create/Edit user form in a Drawer – merkezi validasyon, backend contract uyumlu.
 * Edit: form is filled only from detail API response (user prop). Never use list row data.
 * When open in edit mode: show loading until user is loaded, then setFieldsValue(user); on close parent clears selectedUserId.
 *
 * Role assignment (catalog-driven, assigned-only for checked):
 * - fullRoleCatalog = roleOptions (from parent; GET /api/UserManagement/roles). Source of truth for visible list.
 * - assignedRoleIds = user?.role ? [user.role] : [] (single-role model). Source of truth for checked state.
 * - Render: full catalog as options; checked = assignedRoleIds.includes(role) → form field "role" holds the single selected value.
 * - User switch: form key edit-${user.id} resets component; sync effect sets values from new user (no stale selection).
 */
import React, { useEffect, useMemo } from 'react';
import { Drawer, Form, Input, Radio, Button, Space, Spin, Alert, Typography, Divider } from 'antd';
import type { UserInfo } from '@/api/generated/model';
import type { CreateUserRequest, UpdateUserRequest } from '@/api/generated/model';
import { TenantMembershipManager, membershipsToManagerRows } from '@/features/users/components/TenantMembershipManager';
import { UserTenantSummary } from '@/features/users/components/UserTenantSummary';
import { useAdminUserTenants } from '@/features/users/hooks/useAdminUserTenants';
import { useI18n } from '@/i18n';
import { usersCopy } from '../constants/copy';
import { createUsersFormRules, NAME_MAX_LENGTH, EMAIL_MAX_LENGTH, SHORT_FIELD_MAX_LENGTH, NOTES_MAX_LENGTH } from '../constants/validation';

type Mode = 'create' | 'edit';

/** Platform admins are SuperAdmin only and are not assigned to tenants. */
export type UserFormCreateVariant = 'default' | 'platform';

type Props = {
  open: boolean;
  onClose: () => void;
  mode: Mode;
  createVariant?: UserFormCreateVariant;
  /** For edit: full user from GET /api/UserManagement/{id} (includes Notes). Undefined while loading. */
  user?: UserInfo | null;
  /** True while fetching user in edit mode; form shows loading until user is set. */
  initialLoading?: boolean;
  /** When edit mode and detail fetch failed; show error and retry. */
  fetchError?: unknown;
  /** Retry callback for detail fetch (edit mode). */
  onRetryFetch?: () => void;
  roleOptions: { value: string; label: string }[];
  /** When true and roleOptions empty, role field shows loading (catalog-driven; never show subset). */
  rolesLoading?: boolean;
  onSubmit: (values: CreateUserRequest | UpdateUserRequest) => void;
  loading?: boolean;
  /** Super Admin: open tenant membership manager (edit mode only). */
  canManageTenants?: boolean;
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
  createVariant = 'default',
  user,
  initialLoading = false,
  fetchError = null,
  onRetryFetch,
  roleOptions,
  rolesLoading = false,
  onSubmit,
  loading = false,
  canManageTenants = false,
}: Props) {
  const { t } = useI18n();
  const [form] = Form.useForm();
  const rules = useMemo(() => createUsersFormRules(formRulesContext), []);
  const editUserId = mode === 'edit' && user?.id ? user.id : null;
  const {
    data: memberships = [],
    isLoading: tenantsLoading,
    refetch: refetchTenants,
  } = useAdminUserTenants(editUserId, open && mode === 'edit' && !!editUserId);

  const isPlatformCreate = mode === 'create' && createVariant === 'platform';
  const effectiveRoleOptions = isPlatformCreate
    ? [{ value: 'SuperAdmin', label: usersCopy.roleDisplayName('SuperAdmin') }]
    : roleOptions;

  useEffect(() => {
    if (!open) return;
    if (mode === 'create') {
      form.resetFields();
      if (isPlatformCreate) {
        form.setFieldsValue({ role: 'SuperAdmin' });
      }
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

  const title =
    mode === 'create'
      ? isPlatformCreate
        ? usersCopy.createPlatformUser
        : usersCopy.createUser
      : usersCopy.editUser;
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
          {isPlatformCreate ? (
            <Alert
              type="info"
              showIcon
              message={usersCopy.platformCreateHint}
              style={{ marginBottom: 16 }}
            />
          ) : null}
          <Form.Item name="role" label={usersCopy.role} rules={rules.role}>
            {rolesLoading && effectiveRoleOptions.length === 0 ? (
              <span style={{ color: 'rgba(0,0,0,0.45)', fontSize: 13 }}>{usersCopy.rolesLoading}</span>
            ) : (
              <Radio.Group
                disabled={isPlatformCreate}
                options={effectiveRoleOptions.map((opt) => ({
                  value: opt.value,
                  label: opt.label ?? usersCopy.roleDisplayName(opt.value),
                }))}
              />
            )}
          </Form.Item>
          <Form.Item name="taxNumber" label={usersCopy.taxNumber} rules={rules.taxNumber}>
            <Input maxLength={SHORT_FIELD_MAX_LENGTH} />
          </Form.Item>
          <Form.Item name="notes" label={usersCopy.notes} rules={rules.notes}>
            <Input.TextArea rows={2} maxLength={NOTES_MAX_LENGTH} showCount />
          </Form.Item>
          {mode === 'edit' && user?.id ? (
            <>
              <Divider style={{ margin: '8px 0 16px' }} />
              <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                {t('users.tabs.tenant.columnTenant')}
              </Typography.Text>
              <Alert
                type="info"
                showIcon
                message={t('users.tenants.editReadOnlyHint')}
                style={{ marginBottom: 12 }}
              />
              <UserTenantSummary
                userRole={user.role}
                memberships={memberships}
                loading={tenantsLoading}
              />
              {canManageTenants && user.role !== 'SuperAdmin' ? (
                <div style={{ marginTop: 16 }}>
                  <TenantMembershipManager
                    userId={user.id}
                    currentTenants={membershipsToManagerRows(memberships)}
                    onSuccess={() => void refetchTenants()}
                  />
                </div>
              ) : null}
            </>
          ) : null}
        </Form>
      ) : null}
    </Drawer>
  );
}
