'use client';

/**
 * Super-admin tenant management table (lazy-loaded from /admin/tenants).
 */
import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, Modal, Select, Space, Switch, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import React, { useCallback, useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { EmptyState } from '@/components/EmptyState';
import { useKeyboardShortcutLabels } from '@/components/KeyboardShortcutsProvider';
import { SkeletonWrapper } from '@/components/Skeleton';
import { VirtualTable } from '@/components/VirtualTable';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  type AdminTenantListItem,
  applyTenantImpersonationSession,
  impersonateAdminTenant,
  listAdminTenants,
  restoreAdminTenant,
  updateAdminTenant,
} from '@/features/super-admin/api/adminTenants';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import { TenantLicenseBadge } from '@/features/super-admin/components/TenantLicenseBadge';
import { TenantStatusBadge } from '@/features/super-admin/components/TenantStatusBadge';
import { TenantTableActions } from '@/features/super-admin/components/TenantTableActions';
import { useCanManageTenantDeletion } from '@/features/super-admin/hooks/useCanManageTenantDeletion';
import { ADMIN_TENANTS_QUERY_KEY } from '@/features/super-admin/utils/invalidateTenantLifecycleQueries';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { useMaintenanceMode } from '@/hooks/useMaintenanceMode';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

function isTenantRowDeleted(row: Pick<AdminTenantListItem, 'status' | 'isActive'>): boolean {
  return row.status === 'deleted' || !row.isActive;
}

type TenantFormValues = {
  name: string;
  slug: string;
  email?: string;
  phone?: string;
  address?: string;
  status?: string;
};

export function TenantsTable() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const router = useRouter();
  const { getShortcutLabel } = useKeyboardShortcutLabels();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [editRow, setEditRow] = useState<AdminTenantListItem | null>(null);
  const [editForm] = Form.useForm<TenantFormValues>();
  const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);
  const [actionTenantId, setActionTenantId] = useState<string | null>(null);

  const closeEditModal = useCallback(() => setEditRow(null), []);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.closeModal, closeEditModal, !!editRow);

  const canAccess = isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const isSuperAdminUser = isSuperAdmin(user?.role);
  const canManageDeletion = useCanManageTenantDeletion();
  const { isMaintenanceMode } = useMaintenanceMode();
  const maintenanceDisabledTooltip = t('maintenance.limitedMode.disabledTooltip');

  const tenantsQuery = useQuery({
    queryKey: [...ADMIN_TENANTS_QUERY_KEY, includeDeleted],
    queryFn: () => listAdminTenants(includeDeleted),
    enabled: canAccess,
  });

  const invalidateTenants = useCallback(
    () => void queryClient.invalidateQueries({ queryKey: ADMIN_TENANTS_QUERY_KEY }),
    [queryClient]
  );

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: TenantFormValues }) =>
      updateAdminTenant(id, {
        name: body.name,
        email: body.email,
        phone: body.phone,
        address: body.address,
        status: body.status,
      }),
    onSuccess: () => {
      message.success(t('tenants.messages.updated'));
      setEditRow(null);
      invalidateTenants();
    },
    onError: () => message.error(t('tenants.messages.saveFailed')),
  });

  const suspendMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) =>
      updateAdminTenant(id, { status }),
    onMutate: ({ id }) => setActionTenantId(id),
    onSettled: () => setActionTenantId(null),
    onSuccess: () => {
      message.success(t('tenants.messages.updated'));
      invalidateTenants();
    },
    onError: () => message.error(t('tenants.messages.saveFailed')),
  });

  const restoreMutation = useMutation({
    mutationFn: (id: string) => restoreAdminTenant(id),
    onMutate: (id) => setActionTenantId(id),
    onSettled: () => setActionTenantId(null),
    onSuccess: () => {
      message.success(t('tenants.messages.restored'));
      invalidateTenants();
    },
    onError: () => message.error(t('tenants.messages.restoreFailed')),
  });

  const impersonateMutation = useMutation({
    mutationFn: (id: string) => impersonateAdminTenant(id),
    onMutate: (id) => setActionTenantId(id),
    onSettled: () => setActionTenantId(null),
    onSuccess: (res) => {
      setImpersonationRedirecting(true);
      applyTenantImpersonationSession(res);
    },
    onError: () => message.error(t('tenants.messages.impersonationFailed')),
  });

  const openEdit = useCallback(
    (row: AdminTenantListItem) => {
      setEditRow(row);
      editForm.setFieldsValue({
        name: row.name,
        slug: row.slug,
        email: row.email ?? undefined,
        phone: row.phone ?? undefined,
        status: row.status,
      });
    },
    [editForm]
  );

  const columns: ColumnsType<AdminTenantListItem> = useMemo(
    () => [
      {
        title: t('tenants.columns.name'),
        dataIndex: 'name',
        key: 'name',
        render: (name: string, row) => {
          const deleted = isTenantRowDeleted(row);
          return (
            <Link
              href={`/admin/tenants/${row.id}`}
              className={deleted ? 'tenant-deleted-name' : undefined}
            >
              {name}
            </Link>
          );
        },
      },
      { title: t('tenants.columns.slug'), dataIndex: 'slug', key: 'slug' },
      {
        title: t('tenants.columns.status'),
        dataIndex: 'status',
        key: 'status',
        render: (status: string) => <TenantStatusBadge status={status} />,
      },
      {
        title: t('tenants.columns.adminUser'),
        dataIndex: 'ownerAdminEmail',
        key: 'ownerAdminEmail',
        render: (v: string | null | undefined) => v ?? '—',
      },
      {
        title: t('tenants.columns.license'),
        key: 'license',
        render: (_, record) => (
          <TenantLicenseBadge
            licenseValidUntilUtc={record.licenseValidUntilUtc}
            licenseKey={record.licenseKey}
          />
        ),
      },
      {
        title: t('tenants.columns.created'),
        dataIndex: 'createdAt',
        key: 'createdAt',
        render: dateColumnRender('short'),
      },
      {
        title: t('tenants.columns.actions'),
        key: 'actions',
        fixed: 'right',
        width: 320,
        render: (_, row) =>
          canManageDeletion ? (
            <TenantTableActions
              tenant={row}
              restorePending={restoreMutation.isPending && actionTenantId === row.id}
              impersonatePending={impersonateMutation.isPending && actionTenantId === row.id}
              suspendPending={suspendMutation.isPending && actionTenantId === row.id}
              onEdit={openEdit}
              onSuspend={(id, status) => suspendMutation.mutate({ id, status })}
              onImpersonate={(id) => impersonateMutation.mutate(id)}
              onRestore={(id) => restoreMutation.mutate(id)}
              onArchiveSuccess={() => invalidateTenants()}
              onPermanentDeleteSuccess={() => invalidateTenants()}
            />
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          ),
      },
    ],
    [
      t,
      openEdit,
      restoreMutation.isPending,
      impersonateMutation.isPending,
      suspendMutation.isPending,
      actionTenantId,
      canManageDeletion,
      invalidateTenants,
      restoreMutation,
      impersonateMutation,
      suspendMutation,
    ]
  );

  const tenantRows = tenantsQuery.data ?? [];

  if (!canAccess) {
    return (
      <AdminPageShell>
        <Alert
          type="error"
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      {impersonationRedirecting ? <ImpersonationRedirectOverlay /> : null}
      <AdminPageHeader
        title={t('tenants.page.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
          { title: t('tenants.page.title'), href: '/admin/tenants' },
        ]}
        actions={
          <Tooltip title={isMaintenanceMode ? maintenanceDisabledTooltip : undefined}>
            <span>
              {isMaintenanceMode ? (
                <Button type="primary" icon={<PlusOutlined />} disabled>
                  {t('tenants.actions.create')}
                </Button>
              ) : (
                <Link href="/admin/tenants/create">
                  <Button
                    type="primary"
                    icon={<PlusOutlined />}
                    title={t('keyboardShortcuts.newTenantWithShortcut', {
                      shortcut: getShortcutLabel('newTenant'),
                    })}
                  >
                    {t('tenants.actions.create')}
                  </Button>
                </Link>
              )}
            </span>
          </Tooltip>
        }
      />

      <Typography.Paragraph type="secondary">{t('tenants.page.subtitle')}</Typography.Paragraph>
      <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
        {t('tenants.page.listHelp')}
      </Typography.Paragraph>

      <Card>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 12,
            marginBottom: 16,
          }}
        >
          <Typography.Text strong>{t('tenants.page.title')}</Typography.Text>
          {isSuperAdminUser ? (
            <Space>
              <span>{t('tenants.filters.includeDeleted')}</span>
              <Switch
                checked={includeDeleted}
                onChange={setIncludeDeleted}
                aria-label={t('tenants.filters.includeDeleted')}
              />
            </Space>
          ) : null}
        </div>
        <SkeletonWrapper type="table" loading={tenantsQuery.isLoading} count={5}>
          <VirtualTable
            rowKey="id"
            loading={tenantsQuery.isFetching && !tenantsQuery.isLoading}
            dataSource={tenantRows}
            columns={columns}
            rowClassName={(record) => (isTenantRowDeleted(record) ? 'tenant-row-deleted' : '')}
            locale={{
              emptyText: (
                <EmptyState
                  title={t('tenants.page.empty')}
                  description={t('tenants.page.emptyDescription')}
                  actionText={isMaintenanceMode ? undefined : t('tenants.actions.create')}
                  onAction={
                    isMaintenanceMode
                      ? undefined
                      : () => router.push('/admin/tenants/create')
                  }
                />
              ),
            }}
            scroll={{ x: 1320 }}
            pagination={{ ...adminTablePaginationDefaults }}
          />
        </SkeletonWrapper>
      </Card>

      <Modal
        title={t('tenants.edit.title')}
        open={!!editRow}
        forceRender
        onCancel={() => setEditRow(null)}
        onOk={() => editForm.submit()}
        confirmLoading={updateMutation.isPending}
      >
        <Form
          form={editForm}
          layout="vertical"
          onFinish={(values) => editRow && updateMutation.mutate({ id: editRow.id, body: values })}
        >
          <Form.Item
            name="name"
            label={t('tenants.fields.name')}
            rules={[{ required: true, message: t('tenants.validation.nameRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item label={t('tenants.fields.slug')}>
            <Input value={editRow?.slug} disabled />
          </Form.Item>
          <Form.Item name="email" label={t('tenants.fields.email')}>
            <Input type="email" />
          </Form.Item>
          <Form.Item name="phone" label={t('tenants.fields.phone')}>
            <Input />
          </Form.Item>
          <Form.Item name="address" label={t('tenants.fields.address')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="status" label={t('tenants.fields.status')}>
            <Select
              options={[
                { value: 'active', label: t('tenants.status.active') },
                { value: 'suspended', label: t('tenants.status.suspended') },
              ]}
            />
          </Form.Item>
        </Form>
      </Modal>
    </AdminPageShell>
  );
}

export default TenantsTable;
