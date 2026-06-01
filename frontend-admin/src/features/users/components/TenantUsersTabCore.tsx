'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useMemo, useState } from 'react';
import { Alert, Button, Empty, Input, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    CrownOutlined,
    EditOutlined,
    KeyOutlined,
    ReloadOutlined,
    SafetyCertificateOutlined,
    ThunderboltOutlined,
    UserAddOutlined,
    UserDeleteOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { AddExistingUserModal } from '@/features/super-admin/components/AddExistingUserModal';
import type { AddExistingUserFormValues } from '@/features/super-admin/components/AddExistingUserModal';
import { QuickUserModal } from '@/features/super-admin/components/QuickUserModal';
import type { QuickUserFormValues } from '@/features/super-admin/components/QuickUserModal';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import { SuperAdminCredentialsGate } from '@/features/super-admin/components/SuperAdminCredentialsGate';
import { TenantUserTable } from '@/features/super-admin/components/TenantUserTable';
import { createQuickUser, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import {
    assignTenantUser,
    listTenantUsers as listAdminTenantUsers,
    removeTenantUser,
    updateTenantUser,
    updateTenantUserRole,
    type TenantUser,
} from '@/features/super-admin/api/tenantUsers';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { CreateUserModal } from '@/features/users/components/CreateUserModal';
import { UserPermissionsModal } from '@/features/users/components/UserPermissionsModal';
import { useCreateUser } from '@/features/users/hooks/useCreateUser';
import { InviteTenantContextBanner } from '@/features/users/components/InviteTenantContextBanner';
import { UserRoleBadge } from '@/features/users/components/UserRoleBadge';
import {
    adminUsersQueryKeys,
    listPlatformUsers,
    listTenantUsers as listScopedTenantUsers,
    removeUserFromTenant,
    tenantRowToTenantUser,
    type TenantUserRow,
} from '@/features/users/api/users';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { isBusinessTenantSlug, isPlatformUserRole } from '@/features/users/utils/userScope';
import { useI18n } from '@/i18n';
import { useDebounce } from '@/hooks/useDebounce';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';

const TENANT_ROLE_FILTER_VALUES = ['Manager', 'Cashier', 'Accountant'] as const;
const TENANT_USERS_QUERY_KEY = ['admin', 'tenant-users'] as const;

export type TenantUsersTabCoreProps = {
    /** When set, uses Super Admin tenant-scoped endpoints (`/api/admin/tenants/{id}/users/*`). */
    tenantId?: string;
    tenant?: AdminTenantListItem | null;
    policy: UsersPolicy;
    roleDisplayLabel: (role: string) => string;
    onEdit?: (userId: string) => void;
};

/** Shared tenant user list + create/remove/role management for `/users` and tenant detail. */
export function TenantUsersTabCore({
    tenantId,
    tenant,
    policy,
    roleDisplayLabel,
    onEdit,
}: TenantUsersTabCoreProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const { canProvisionTenantCredentials: superAdminCredentials } = useSuperAdminPlatformPolicy();
    const isFixedTenant = Boolean(tenantId);
    const canProvision =
        policy.canProvisionTenantCredentials || (isFixedTenant && superAdminCredentials);

    const queryClient = useQueryClient();
    const [tenantIdFilter, setTenantIdFilter] = useState<string | undefined>();
    const [roleFilter, setRoleFilter] = useState<string | undefined>();
    const [search, setSearch] = useState('');
    const debouncedSearch = useDebounce(search, 300);
    const searchParam = debouncedSearch.trim() || undefined;
    const [addOpen, setAddOpen] = useState(false);
    const [createOpen, setCreateOpen] = useState(false);
    const [quickOpen, setQuickOpen] = useState(false);
    const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
    const [quickRole, setQuickRole] = useState('Manager');
    const [resetRow, setResetRow] = useState<TenantUserRow | TenantUser | null>(null);
    const [permissionsUser, setPermissionsUser] = useState<TenantUserRow | TenantUser | null>(null);
    const [roleChangeUserId, setRoleChangeUserId] = useState<string | null>(null);

    const tenantsQuery = useGetApiAdminTenants();
    const { tenants: createTenants, isLoading: createTenantsLoading } = useTenantList();
    const businessTenants = useMemo(
        () => (tenantsQuery.data ?? []).filter((row) => row.isActive && isBusinessTenantSlug(row.slug)),
        [tenantsQuery.data],
    );

    const scopedUsersQuery = useQuery({
        queryKey: adminUsersQueryKeys.tenant(tenantIdFilter, roleFilter, searchParam),
        queryFn: () =>
            listScopedTenantUsers({
                tenantId: tenantIdFilter,
                role: roleFilter || undefined,
                isActive: true,
                search: searchParam,
            }),
        enabled: !isFixedTenant,
        select: (data) => data.map(tenantRowToTenantUser),
    });

    const fixedTenantUsersQuery = useQuery({
        queryKey: [...TENANT_USERS_QUERY_KEY, tenantId],
        queryFn: () => listAdminTenantUsers(tenantId!),
        enabled: isFixedTenant,
    });

    const allUsersQuery = useQuery({
        queryKey: ['admin', 'users', 'picker', 'platform'],
        queryFn: () => listPlatformUsers({ isActive: true }),
        enabled: isFixedTenant && addOpen,
    });

    const invalidate = () => {
        if (isFixedTenant) {
            void queryClient.invalidateQueries({ queryKey: [...TENANT_USERS_QUERY_KEY, tenantId] });
            void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
        } else {
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
        }
    };

    const createMutation = useCreateUser({
        fixedTenantId: tenantId,
        onSuccess: () => {
            setCreateOpen(false);
            invalidate();
        },
    });

    const quickMutation = useMutation({
        mutationFn: (values: QuickUserFormValues) => {
            const targetTenantId = tenantId ?? values.tenantId;
            if (!targetTenantId) throw new Error('tenantId required');
            return createQuickUser(targetTenantId, { role: values.role });
        },
        onSuccess: (res, values) => {
            setQuickOpen(false);
            setQuickRole(values.role);
            setQuickResult(res);
            invalidate();
            message.success(t('tenants.users.quick.messages.created'));
        },
        onError: (err: unknown) => {
            const status = (err as { response?: { status?: number } })?.response?.status;
            if (status === 429) {
                message.error(t('tenants.users.quick.messages.rateLimited'));
                return;
            }
            message.error(t('tenants.users.quick.messages.failed'));
        },
    });

    const addMutation = useMutation({
        mutationFn: (values: AddExistingUserFormValues) =>
            assignTenantUser(tenantId!, {
                userId: values.userId,
                role: values.role,
                isOwner: values.isOwner,
            }),
        onSuccess: () => {
            message.success(t('tenants.users.messages.added'));
            setAddOpen(false);
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.addFailed')),
    });

    const removeMutation = useMutation({
        mutationFn: (userId: string) => {
            const row = scopedUsersQuery.data?.find((r) => r.userId === userId);
            const targetTenantId = tenantId ?? row?.tenantId;
            if (!targetTenantId) throw new Error('tenantId required');
            return isFixedTenant
                ? removeTenantUser(targetTenantId, userId)
                : removeUserFromTenant(targetTenantId, userId);
        },
        onSuccess: () => {
            message.success(
                isFixedTenant ? t('tenants.users.messages.removed') : t('users.tabs.tenant.removedSuccess'),
            );
            invalidate();
        },
        onError: () =>
            message.error(
                isFixedTenant ? t('tenants.users.messages.removeFailed') : t('users.list.errorLoad'),
            ),
    });

    const setOwnerMutation = useMutation({
        mutationFn: (userId: string) => updateTenantUser(tenantId!, userId, { isOwner: true }),
        onSuccess: () => {
            message.success(t('tenants.users.messages.ownerSet'));
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.ownerSetFailed')),
    });

    const roleMutation = useMutation({
        mutationFn: ({ userId, role }: { userId: string; role: string }) =>
            updateTenantUserRole(tenantId!, userId, role),
        onMutate: ({ userId }) => setRoleChangeUserId(userId),
        onSettled: () => setRoleChangeUserId(null),
        onSuccess: () => {
            message.success(t('tenants.users.messages.roleUpdated'));
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.roleUpdateFailed')),
    });

    const scopedRows = scopedUsersQuery.data ?? [];
    const filteredScopedRows = useMemo(() => {
        const q = searchParam?.toLowerCase();
        if (!q) return scopedRows;
        return scopedRows.filter((row) => {
            if (tenantIdFilter && row.tenantId !== tenantIdFilter) return false;
            if (roleFilter && row.role !== roleFilter) return false;
            return (
                row.name.toLowerCase().includes(q) ||
                row.userName.toLowerCase().includes(q) ||
                row.email.toLowerCase().includes(q) ||
                row.tenantSlug.toLowerCase().includes(q) ||
                row.role.toLowerCase().includes(q)
            );
        });
    }, [scopedRows, searchParam, tenantIdFilter, roleFilter]);

    const filteredFixedTenantUsers = useMemo(() => {
        const users = fixedTenantUsersQuery.data ?? [];
        const q = searchParam?.toLowerCase();
        if (!q) return users;
        return users.filter(
            (u) =>
                u.name.toLowerCase().includes(q) ||
                u.userName.toLowerCase().includes(q) ||
                u.email.toLowerCase().includes(q) ||
                u.role.toLowerCase().includes(q),
        );
    }, [fixedTenantUsersQuery.data, searchParam]);

    const tenantFilterOptions = useMemo(
        () => [
            { value: '', label: t('users.tabs.tenant.filterAll') },
            ...businessTenants
                .map((row) => ({
                    value: row.id,
                    label: t('users.create.tenantOption', { name: row.name, slug: row.slug }),
                }))
                .sort((a, b) => a.label.localeCompare(b.label)),
        ],
        [businessTenants, t],
    );

    const assignedIds = useMemo(
        () => new Set((fixedTenantUsersQuery.data ?? []).map((u) => u.userId)),
        [fixedTenantUsersQuery.data],
    );

    const userPickerOptions = useMemo(() => {
        const list = Array.isArray(allUsersQuery.data) ? allUsersQuery.data : [];
        return list
            .filter((u) => u.id && !assignedIds.has(u.id) && u.role !== 'SuperAdmin')
            .map((u) => {
                const label = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.userName || u.id;
                return {
                    value: u.id!,
                    label: `${label} (${u.email ?? u.userName ?? u.id})`,
                };
            });
    }, [allUsersQuery.data, assignedIds]);

    const scopedColumns: ColumnsType<TenantUserRow> = useMemo(
        () => [
            {
                title: t('users.tabs.tenant.columnTenant'),
                dataIndex: 'tenantSlug',
                key: 'tenantSlug',
                render: (slug: string, row) => (
                    <Space orientation="vertical" size={0}>
                        <Tag color="blue">{slug}</Tag>
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            {row.tenantName}
                        </Typography.Text>
                    </Space>
                ),
                sorter: (a, b) => a.tenantSlug.localeCompare(b.tenantSlug),
            },
            {
                title: t('users.list.columnName'),
                dataIndex: 'name',
                key: 'name',
                render: (name: string, row) => (
                    <Space orientation="vertical" size={0}>
                        <span style={{ fontWeight: 600 }}>{name}</span>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {row.email}
                        </Typography.Text>
                    </Space>
                ),
            },
            {
                title: t('users.list.columnUserName'),
                dataIndex: 'userName',
                key: 'userName',
                width: 140,
                ellipsis: true,
                render: (userName: string) => userName?.trim() || '—',
                sorter: (a, b) =>
                    (a.userName ?? '').localeCompare(b.userName ?? '', undefined, { sensitivity: 'base' }),
            },
            {
                title: t('users.list.columnRole'),
                dataIndex: 'role',
                key: 'role',
                render: (role: string, row) => <UserRoleBadge role={role} isOwner={row.isOwner} />,
            },
            {
                title: t('users.list.columnStatus'),
                key: 'status',
                render: () => <Tag color="green">{t('users.list.statusActive')}</Tag>,
            },
            {
                title: t('users.list.columnActions'),
                key: 'actions',
                render: (_: unknown, row) => (
                    <Space wrap size="small">
                        {policy.canEdit && onEdit ? (
                            <Button size="small" icon={<EditOutlined />} onClick={() => onEdit(row.userId)}>
                                {t('users.list.edit')}
                            </Button>
                        ) : null}
                        {policy.canManagePermissions && !isPlatformUserRole(row.role) ? (
                            <Button
                                size="small"
                                icon={<SafetyCertificateOutlined />}
                                onClick={() => setPermissionsUser(row)}
                            >
                                {t('users.permissionsModal.action')}
                            </Button>
                        ) : null}
                        {canProvision && policy.canResetPassword(row.role) ? (
                            <Button size="small" icon={<KeyOutlined />} onClick={() => setResetRow(row)}>
                                {t('users.list.resetPassword')}
                            </Button>
                        ) : null}
                        {policy.canEdit ? (
                            <Popconfirm
                                title={t('users.tabs.tenant.confirmRemove.title')}
                                description={t('users.tabs.tenant.confirmRemove.body', {
                                    tenant: row.tenantSlug,
                                })}
                                onConfirm={() => removeMutation.mutate(row.userId)}
                            >
                                <Button
                                    size="small"
                                    danger
                                    icon={<UserDeleteOutlined />}
                                    loading={removeMutation.isPending}
                                >
                                    {t('users.tabs.tenant.removeFromTenant')}
                                </Button>
                            </Popconfirm>
                        ) : null}
                    </Space>
                ),
            },
        ],
        [t, policy, canProvision, removeMutation.isPending, onEdit],
    );

    const listLoading = isFixedTenant ? fixedTenantUsersQuery.isLoading : scopedUsersQuery.isLoading;
    const listFetching = isFixedTenant ? fixedTenantUsersQuery.isFetching : scopedUsersQuery.isFetching;
    const listError = isFixedTenant ? fixedTenantUsersQuery.isError : scopedUsersQuery.isError;
    const refetchList = () => (isFixedTenant ? fixedTenantUsersQuery.refetch() : scopedUsersQuery.refetch());

    return (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            {!isFixedTenant ? (
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('users.tabs.tenant.description')}
                </Typography.Paragraph>
            ) : null}
            {tenant ? <InviteTenantContextBanner tenant={tenant} variant="page" /> : null}

            <Space wrap>
                <Button icon={<ReloadOutlined />} onClick={() => refetchList()} loading={listFetching}>
                    {t('common.buttons.refresh')}
                </Button>
                {isFixedTenant ? (
                    <Button icon={<UserAddOutlined />} onClick={() => setAddOpen(true)}>
                        {t('tenants.users.actions.add')}
                    </Button>
                ) : null}
                {canProvision ? (
                    <>
                        <Button type="primary" icon={<UserAddOutlined />} onClick={() => setCreateOpen(true)}>
                            {isFixedTenant ? t('tenants.users.create.action') : t('users.create.action')}
                        </Button>
                        {isFixedTenant ? (
                            <SuperAdminCredentialsGate showRestrictedHint={false}>
                                <Button icon={<ThunderboltOutlined />} onClick={() => setQuickOpen(true)}>
                                    {t('tenants.users.quick.action')}
                                </Button>
                            </SuperAdminCredentialsGate>
                        ) : null}
                    </>
                ) : null}
                {!isFixedTenant ? (
                    <>
                        <Select
                            style={{ minWidth: 220 }}
                            value={tenantIdFilter ?? ''}
                            onChange={(v) => setTenantIdFilter(v || undefined)}
                            options={tenantFilterOptions}
                        />
                        <Select
                            allowClear
                            placeholder={t('users.tabs.tenant.filterRole')}
                            style={{ minWidth: 160 }}
                            value={roleFilter}
                            onChange={setRoleFilter}
                            options={TENANT_ROLE_FILTER_VALUES.map((role) => ({
                                value: role,
                                label: roleDisplayLabel(role),
                            }))}
                        />
                        <Input.Search
                            allowClear
                            placeholder={t('users.tabs.tenant.searchPlaceholder')}
                            style={{ width: 280 }}
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                            onSearch={setSearch}
                        />
                    </>
                ) : null}
                {isFixedTenant ? (
                    <Input.Search
                        allowClear
                        placeholder={t('users.tabs.tenant.searchPlaceholder')}
                        style={{ width: 280 }}
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        onSearch={setSearch}
                    />
                ) : null}
            </Space>

            {isFixedTenant && !canProvision ? <SuperAdminCredentialsGate /> : null}

            {listError ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('users.list.errorLoad')}
                    action={
                        <Button size="small" onClick={() => refetchList()}>
                            {t('users.list.retry')}
                        </Button>
                    }
                />
            ) : null}

            {isFixedTenant ? (
                <TenantUserTable
                    users={filteredFixedTenantUsers}
                    loading={listLoading}
                    setOwnerPending={setOwnerMutation.isPending}
                    removePending={removeMutation.isPending}
                    roleChangeUserId={roleChangeUserId}
                    onSetOwner={(userId) => setOwnerMutation.mutate(userId)}
                    onRemove={(userId) => removeMutation.mutate(userId)}
                    onRoleChange={(userId, role) => roleMutation.mutate({ userId, role })}
                    onResetPassword={
                        canProvision
                            ? (userId) => {
                                  const row =
                                      fixedTenantUsersQuery.data?.find((u) => u.userId === userId) ?? null;
                                  setResetRow(row);
                              }
                            : undefined
                    }
                />
            ) : (
                <Table
                    rowKey={(r) => `${r.tenantId}:${r.userId}`}
                    loading={listLoading}
                    dataSource={filteredScopedRows}
                    columns={scopedColumns}
                    pagination={{ pageSize: 20, showSizeChanger: true, pageSizeOptions: [10, 20, 50] }}
                    locale={{
                        emptyText: (
                            <Empty
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                                description={t('users.tabs.tenant.empty')}
                            />
                        ),
                    }}
                />
            )}

            {isFixedTenant ? (
                <AddExistingUserModal
                    open={addOpen}
                    confirmLoading={addMutation.isPending}
                    loadingUsers={allUsersQuery.isLoading}
                    userOptions={userPickerOptions}
                    onClose={() => setAddOpen(false)}
                    onSubmit={(values) => addMutation.mutate(values)}
                />
            ) : null}

            <CreateUserModal
                open={createOpen}
                variant={isFixedTenant ? 'tenantDetail' : 'usersPage'}
                isSuperAdmin={!isFixedTenant}
                tenantId={tenantId}
                tenantRows={createTenants}
                tenantsLoading={createTenantsLoading}
                showOwnerToggle={isFixedTenant}
                confirmLoading={createMutation.isPending}
                onClose={() => setCreateOpen(false)}
                onComplete={() => setCreateOpen(false)}
                onSubmit={(values) => createMutation.mutateAsync(values)}
            />

            {canProvision ? (
                <>
                    {isFixedTenant ? (
                        <QuickUserModal
                            open={quickOpen}
                            variant="tenantDetail"
                            tenantId={tenantId}
                            tenantSlug={tenant?.slug ?? 'tenant'}
                            tenantName={tenant?.name}
                            confirmLoading={quickMutation.isPending}
                            onClose={() => setQuickOpen(false)}
                            onSubmit={(values) => quickMutation.mutate(values)}
                        />
                    ) : null}

                    <ResetPasswordModal
                        open={!!resetRow}
                        tenantId={
                            tenantId ??
                            (resetRow && 'tenantId' in resetRow ? resetRow.tenantId : '') ??
                            ''
                        }
                        user={
                            resetRow
                                ? {
                                      userId: resetRow.userId,
                                      email: resetRow.email,
                                      name: resetRow.name,
                                      role: resetRow.role,
                                      isOwner: resetRow.isOwner,
                                      joinedAtUtc: resetRow.joinedAtUtc,
                                  }
                                : null
                        }
                        onClose={() => setResetRow(null)}
                        onCompleted={() => refetchList()}
                    />

                    {isFixedTenant ? (
                        <QuickUserSuccessModal
                            open={!!quickResult}
                            result={quickResult}
                            role={quickRole}
                            tenantName={tenant?.name ?? tenantId ?? ''}
                            tenantSlug={tenant?.slug ?? 'tenant'}
                            onClose={() => setQuickResult(null)}
                            onGenerateAnother={() => {
                                setQuickResult(null);
                                setQuickOpen(true);
                            }}
                        />
                    ) : null}
                </>
            ) : null}

            {permissionsUser ? (
                <UserPermissionsModal
                    open
                    userId={permissionsUser.userId}
                    userName={permissionsUser.name?.trim() || permissionsUser.userName?.trim() || permissionsUser.email}
                    userRole={permissionsUser.role}
                    onClose={() => setPermissionsUser(null)}
                />
            ) : null}
        </Space>
    );
}
