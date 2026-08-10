'use client';

/**
 * Super-admin tenant management table (lazy-loaded from /admin/tenants).
 */
import { DownloadOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Switch,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
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
  type AdminTenantListQuery,
  applyTenantImpersonationSession,
  exportTenantsCsv,
  impersonateAdminTenant,
  listAdminTenantsPaged,
  restoreAdminTenant,
  updateAdminTenant,
  updateTenantStatus,
} from '@/features/super-admin/api/adminTenants';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import { TenantLicenseBadge } from '@/features/super-admin/components/TenantLicenseBadge';
import { TenantStatusBadge } from '@/features/super-admin/components/TenantStatusBadge';
import { TenantTableActions } from '@/features/super-admin/components/TenantTableActions';
import { useCanManageTenantDeletion } from '@/features/super-admin/hooks/useCanManageTenantDeletion';
import { ADMIN_TENANTS_QUERY_KEY } from '@/features/super-admin/utils/invalidateTenantLifecycleQueries';
import { isTenantRemovedStatus } from '@/features/super-admin/utils/tenantStatusLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useDebounce } from '@/hooks/useDebounce';
import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { useMaintenanceMode } from '@/hooks/useMaintenanceMode';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

function isTenantRowDeleted(row: Pick<AdminTenantListItem, 'status' | 'isActive'>): boolean {
  return isTenantRemovedStatus(row.status) || !row.isActive;
}

const STATUS_OPTIONS = [
  'lead',
  'in_onboarding',
  'active',
  'suspended',
  'cancelled',
  'archived',
] as const;

const LICENSE_TYPE_OPTIONS = ['Trial', 'Starter', 'Business', 'Plus'] as const;

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
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [licenseTypeFilter, setLicenseTypeFilter] = useState<
    'Trial' | 'Starter' | 'Business' | 'Plus' | undefined
  >();
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 300);
  const [sortBy, setSortBy] = useState('CreatedAt');
  const [sortOrder, setSortOrder] = useState<'Asc' | 'Desc'>('Desc');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
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

  const listQuery: AdminTenantListQuery = useMemo(
    () => ({
      includeDeleted,
      status: statusFilter,
      licenseType: licenseTypeFilter,
      search: debouncedSearch.trim() || undefined,
      sortBy,
      sortOrder,
      page,
      pageSize,
    }),
    [
      includeDeleted,
      statusFilter,
      licenseTypeFilter,
      debouncedSearch,
      sortBy,
      sortOrder,
      page,
      pageSize,
    ]
  );

  const tenantsQuery = useQuery({
    queryKey: [...ADMIN_TENANTS_QUERY_KEY, listQuery],
    queryFn: () => listAdminTenantsPaged(listQuery),
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

  const statusMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) => updateTenantStatus(id, status),
    onMutate: ({ id }) => setActionTenantId(id),
    onSettled: () => setActionTenantId(null),
    onSuccess: () => {
      message.success(t('tenants.messages.updated'));
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

  const exportMutation = useMutation({
    mutationFn: () => exportTenantsCsv(listQuery),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `tenants_export_${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      message.success(t('tenants.actions.exportSuccess'));
    },
    onError: () => message.error(t('tenants.actions.exportFailed')),
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

  const statusSelectOptions = useMemo(
    () =>
      STATUS_OPTIONS.map((value) => ({
        value,
        label: t(`tenants.status.${value}` as 'tenants.status.active'),
      })),
    [t]
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
        render: (status: string, row) =>
          isSuperAdminUser && !isTenantRemovedStatus(status) ? (
            <Select
              size="small"
              style={{ minWidth: 140 }}
              value={status}
              options={statusSelectOptions}
              loading={statusMutation.isPending && actionTenantId === row.id}
              onChange={(next) => statusMutation.mutate({ id: row.id, status: next })}
              aria-label={t('tenants.actions.changeStatus')}
            />
          ) : (
            <TenantStatusBadge status={status} />
          ),
      },
      {
        title: t('tenants.columns.licenseType'),
        dataIndex: 'licenseType',
        key: 'licenseType',
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
        title: t('tenants.columns.registers'),
        dataIndex: 'registerCount',
        key: 'registerCount',
        width: 90,
        render: (v: number | undefined) => v ?? 0,
      },
      {
        title: t('tenants.columns.users'),
        dataIndex: 'userCount',
        key: 'userCount',
        width: 90,
        render: (v: number | undefined) => v ?? 0,
      },
      {
        title: t('tenants.columns.adminUser'),
        dataIndex: 'ownerAdminEmail',
        key: 'ownerAdminEmail',
        render: (v: string | null | undefined) => v ?? '—',
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
      statusMutation.isPending,
      statusSelectOptions,
      actionTenantId,
      canManageDeletion,
      isSuperAdminUser,
      invalidateTenants,
      restoreMutation,
      impersonateMutation,
      suspendMutation,
      statusMutation,
    ]
  );

  const tenantRows = tenantsQuery.data?.items ?? [];
  const totalCount = tenantsQuery.data?.totalCount ?? 0;

  const pagination: TablePaginationConfig = {
    ...adminTablePaginationDefaults,
    current: page,
    pageSize,
    total: totalCount,
    onChange: (nextPage, nextSize) => {
      setPage(nextPage);
      setPageSize(nextSize);
    },
  };

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
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'administration', {
          title: t('tenants.page.title'),
        })}
        actions={
          <Space wrap>
            {isSuperAdminUser ? (
              <Button
                icon={<DownloadOutlined />}
                loading={exportMutation.isPending}
                onClick={() => exportMutation.mutate()}
              >
                {t('tenants.actions.exportCsv')}
              </Button>
            ) : null}
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
          </Space>
        }
      />

      <Typography.Paragraph type="secondary">{t('tenants.page.subtitle')}</Typography.Paragraph>
      <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
        {t('tenants.page.listHelp')}
      </Typography.Paragraph>

      <Card>
        <Space wrap style={{ marginBottom: 16, width: '100%' }} size="middle">
          <Input.Search
            allowClear
            placeholder={t('tenants.filters.searchPlaceholder')}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            style={{ width: 240 }}
          />
          <Select
            allowClear
            placeholder={t('tenants.filters.statusPlaceholder')}
            value={statusFilter}
            onChange={(v) => {
              setStatusFilter(v);
              setPage(1);
            }}
            options={statusSelectOptions}
            style={{ width: 180 }}
          />
          <Select
            allowClear
            placeholder={t('tenants.filters.licenseTypePlaceholder')}
            value={licenseTypeFilter}
            onChange={(v) => {
              setLicenseTypeFilter(v);
              setPage(1);
            }}
            options={LICENSE_TYPE_OPTIONS.map((value) => ({ value, label: value }))}
            style={{ width: 160 }}
          />
          <Select
            placeholder={t('tenants.filters.sortByPlaceholder')}
            value={`${sortBy}:${sortOrder}`}
            onChange={(v) => {
              const [by, order] = v.split(':');
              setSortBy(by);
              setSortOrder(order === 'Asc' ? 'Asc' : 'Desc');
              setPage(1);
            }}
            options={[
              { value: 'Name:Asc', label: t('tenants.filters.sortName') },
              { value: 'CreatedAt:Desc', label: t('tenants.filters.sortCreated') },
              { value: 'LicenseDaysLeft:Asc', label: t('tenants.filters.sortLicenseDays') },
              { value: 'RegisterCount:Desc', label: t('tenants.filters.sortRegisters') },
              { value: 'UserCount:Desc', label: t('tenants.filters.sortUsers') },
              { value: 'LastActivity:Desc', label: t('tenants.filters.sortLastActivity') },
            ]}
            style={{ width: 200 }}
          />
          {isSuperAdminUser ? (
            <Space>
              <span>{t('tenants.filters.includeDeleted')}</span>
              <Switch
                checked={includeDeleted}
                onChange={(v) => {
                  setIncludeDeleted(v);
                  setPage(1);
                }}
                aria-label={t('tenants.filters.includeDeleted')}
              />
            </Space>
          ) : null}
        </Space>

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
                    isMaintenanceMode ? undefined : () => router.push('/admin/tenants/create')
                  }
                />
              ),
            }}
            scroll={{ x: 1600 }}
            pagination={pagination}
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
            <Select options={statusSelectOptions} />
          </Form.Item>
        </Form>
      </Modal>
    </AdminPageShell>
  );
}

export default TenantsTable;
