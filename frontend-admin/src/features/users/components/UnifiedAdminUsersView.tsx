'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Collapse,
    Empty,
    Flex,
    Input,
    Popconfirm,
    Select,
    Space,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    EditOutlined,
    KeyOutlined,
    MailOutlined,
    ReloadOutlined,
    SearchOutlined,
    ThunderboltOutlined,
    StopOutlined,
    CheckCircleOutlined,
    EyeOutlined,
    UserDeleteOutlined,
    UserOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';

import { InviteUserModal } from '@/features/users/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/users/components/InviteUserModal';
import { UserCreatedSuccessModal } from '@/features/super-admin/components/UserCreatedSuccessModal';
import { QuickUserModal } from '@/features/super-admin/components/QuickUserModal';
import type { QuickUserFormValues } from '@/features/super-admin/components/QuickUserModal';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { createQuickUser, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { createTenantUser, type CreateTenantUserResult } from '@/features/super-admin/api/tenantUsers';
import { UserInvitationsPanel } from '@/features/users/components/UserInvitationsPanel';
import { UserRoleBadge } from '@/features/users/components/UserRoleBadge';
import { UserTypeBadge } from '@/features/users/components/UserTypeBadge';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import {
    adminUserToUserInfo,
    adminUsersQueryKeys,
    listPlatformUsers,
    listTenantUsers,
    removeUserFromTenant,
    tenantRowToTenantUser,
    type TenantUserRow,
} from '@/features/users/api/users';
import type { UserInfo } from '@/features/users/api/usersGateway';
import { TenantFilter } from '@/features/users/components/TenantFilter';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import {
    ADMIN_USERS_FILTER_ALL,
    ADMIN_USERS_FILTER_PLATFORM,
    ADMIN_USERS_PAGE_PATH,
    buildAdminUsersPageHref,
    resolveAdminUsersTenantFilterFromSearchParams,
    tenantFilterFromUiValue,
    tenantFilterToUiValue,
} from '@/features/users/utils/adminUsersPageUrl';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import type { UnifiedAdminUserRow } from '@/features/users/types/unifiedAdminUserRow';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';

export type { UnifiedAdminUserRow } from '@/features/users/types/unifiedAdminUserRow';

const TENANT_ROLE_FILTER_VALUES = ['Manager', 'Cashier', 'Accountant'] as const;
const FILTER_ALL = ADMIN_USERS_FILTER_ALL;
const FILTER_PLATFORM = ADMIN_USERS_FILTER_PLATFORM;

function platformDisplayName(user: UserInfo): string {
    const first = user.firstName ?? '';
    const last = user.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || user.userName || user.id || '—';
}

export type UnifiedAdminUsersViewProps = {
    policy: UsersPolicy;
    roleDisplayLabel: (role: string) => string;
    currentUserId?: string | null;
    onView: (user: UserInfo) => void;
    onEdit: (userId: string) => void;
    onDeactivate: (user: UserInfo) => void;
    onReactivate: (user: UserInfo) => void;
    onResetPassword: (user: UserInfo) => void;
    onCreatePlatformUser: () => void;
};

/** Single super-admin user list: platform + tenant memberships with tenant filter. */
export function UnifiedAdminUsersView({
    policy,
    roleDisplayLabel,
    currentUserId,
    onView,
    onEdit,
    onDeactivate,
    onReactivate,
    onResetPassword,
    onCreatePlatformUser,
}: UnifiedAdminUsersViewProps) {
    const { t, formatLocale } = useI18n();
    const router = useRouter();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();

    const [selectedTenant, setSelectedTenant] = useState(() =>
        tenantFilterToUiValue(resolveAdminUsersTenantFilterFromSearchParams(searchParams)),
    );
    const tenantFilter = tenantFilterFromUiValue(selectedTenant);
    const [roleFilter, setRoleFilter] = useState<string | undefined>();
    const [statusFilter, setStatusFilter] = useState<boolean | undefined>(true);
    const [search, setSearch] = useState('');
    const [searchInput, setSearchInput] = useState('');
    const [inviteOpen, setInviteOpen] = useState(false);
    const [quickOpen, setQuickOpen] = useState(false);
    const [createResult, setCreateResult] = useState<CreateTenantUserResult | null>(null);
    const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
    const [quickRole, setQuickRole] = useState('Manager');
    const [quickTenantCtx, setQuickTenantCtx] = useState<{ name: string; slug: string } | null>(null);
    const [resetRow, setResetRow] = useState<TenantUserRow | null>(null);

    useEffect(() => {
        setSelectedTenant(
            tenantFilterToUiValue(resolveAdminUsersTenantFilterFromSearchParams(searchParams)),
        );
    }, [searchParams]);

    const inviteFixedTenantId =
        tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM ? tenantFilter : undefined;

    const handleTenantFilterChange = (value: string) => {
        setSelectedTenant(value);
        const filter = tenantFilterFromUiValue(value);
        if (filter === FILTER_ALL) {
            router.replace(ADMIN_USERS_PAGE_PATH, { scroll: false });
            return;
        }
        if (filter === FILTER_PLATFORM) {
            router.replace(`${ADMIN_USERS_PAGE_PATH}?filter=platform`, { scroll: false });
            return;
        }
        router.replace(buildAdminUsersPageHref(filter), { scroll: false });
    };

    const { tenants: inviteTenants, isLoading: inviteTenantsLoading } = useTenantList();

    const quickFixedTenant = useMemo(
        () =>
            inviteFixedTenantId
                ? inviteTenants.find((row) => row.id === inviteFixedTenantId)
                : undefined,
        [inviteFixedTenantId, inviteTenants],
    );

    const platformQuery = useQuery({
        queryKey: adminUsersQueryKeys.platform(statusFilter),
        queryFn: () => listPlatformUsers(statusFilter != null ? { isActive: statusFilter } : undefined),
        select: (data) => data.map(adminUserToUserInfo),
    });

    const tenantApiTenantId =
        tenantFilter && tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM
            ? tenantFilter
            : undefined;

    const tenantUsersQuery = useQuery({
        queryKey: adminUsersQueryKeys.tenant(tenantApiTenantId, roleFilter),
        queryFn: () =>
            listTenantUsers({
                tenantId: tenantApiTenantId,
                role: roleFilter || undefined,
                isActive: statusFilter,
            }),
        enabled: tenantFilter !== FILTER_PLATFORM,
        select: (data) => data.map(tenantRowToTenantUser),
    });

    const statusFilterOptions = useMemo(
        () => [
            { value: 'active', label: t('users.list.statusActive') },
            { value: 'inactive', label: t('users.list.statusInactive') },
        ],
        [t],
    );

    const unifiedRows = useMemo((): UnifiedAdminUserRow[] => {
        const platformTenantLabel = t('users.unified.filterPlatformOnly');
        const rows: UnifiedAdminUserRow[] = [];

        if (tenantFilter === FILTER_ALL || tenantFilter === FILTER_PLATFORM) {
            for (const user of platformQuery.data ?? []) {
                if (!user.id) continue;
                rows.push({
                    kind: 'platform',
                    key: `platform:${user.id}`,
                    tenantSlug: platformTenantLabel,
                    tenantName: platformTenantLabel,
                    userId: user.id,
                    name: platformDisplayName(user),
                    email: user.email ?? user.userName ?? '—',
                    role: user.role ?? '—',
                    isActive: user.isActive,
                    lastLoginAt: user.lastLoginAt ?? null,
                    user,
                });
            }
        }

        if (tenantFilter !== FILTER_PLATFORM) {
            for (const row of tenantUsersQuery.data ?? []) {
                rows.push({
                    kind: 'tenant',
                    key: `tenant:${row.tenantId}:${row.userId}`,
                    tenantSlug: row.tenantSlug,
                    tenantName: row.tenantName,
                    tenantId: row.tenantId,
                    userId: row.userId,
                    name: row.name,
                    email: row.email,
                    role: row.role,
                    isActive: row.isActive,
                    isOwner: row.isOwner,
                    lastLoginAt: row.lastLoginAt ?? null,
                    row,
                });
            }
        }

        return rows;
    }, [platformQuery.data, tenantUsersQuery.data, tenantFilter, t]);

    const filteredRows = useMemo(() => {
        const q = search.trim().toLowerCase();
        if (!q) return unifiedRows;
        return unifiedRows.filter((row) => {
            if (roleFilter && row.role !== roleFilter) return false;
            return (
                row.name.toLowerCase().includes(q) ||
                row.email.toLowerCase().includes(q) ||
                row.tenantName.toLowerCase().includes(q) ||
                row.tenantSlug.toLowerCase().includes(q) ||
                row.role.toLowerCase().includes(q)
            );
        });
    }, [unifiedRows, search, roleFilter]);

    const createMutation = useMutation({
        mutationFn: (values: InviteUserFormValues) => {
            if (!values.tenantId) throw new Error('tenantId required');
            return createTenantUser(values.tenantId, {
                email: values.email.trim(),
                role: values.role,
                isOwner: values.isOwner,
            });
        },
        onSuccess: (res) => {
            setCreateResult(res);
            setInviteOpen(false);
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.platform() });
            message.success(t('tenants.users.invite.messages.created'));
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
    });

    const quickMutation = useMutation({
        mutationFn: (values: QuickUserFormValues) => {
            const tenantId = values.tenantId ?? inviteFixedTenantId;
            if (!tenantId) throw new Error('tenantId required');
            return createQuickUser(tenantId, { role: values.role });
        },
        onSuccess: (res, values) => {
            const tenantId = values.tenantId ?? inviteFixedTenantId;
            const tenant = inviteTenants.find((row) => row.id === tenantId);
            setQuickTenantCtx({
                name: tenant?.name ?? tenantId ?? '',
                slug: tenant?.slug ?? 'tenant',
            });
            setQuickOpen(false);
            setQuickRole(values.role);
            setQuickResult(res);
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
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

    const removeMutation = useMutation({
        mutationFn: ({ tenantId, userId }: { tenantId: string; userId: string }) =>
            removeUserFromTenant(tenantId, userId),
        onSuccess: () => {
            message.success(t('users.tabs.tenant.removedSuccess'));
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
        },
        onError: () => message.error(t('users.list.errorLoad')),
    });

    const isLoading =
        (tenantFilter !== FILTER_PLATFORM && tenantUsersQuery.isLoading) ||
        ((tenantFilter === FILTER_ALL || tenantFilter === FILTER_PLATFORM) && platformQuery.isLoading);

    const isFetching = platformQuery.isFetching || tenantUsersQuery.isFetching;
    const isError = platformQuery.isError || tenantUsersQuery.isError;

    const refetchAll = () => {
        void platformQuery.refetch();
        void tenantUsersQuery.refetch();
    };

    const columns: ColumnsType<UnifiedAdminUserRow> = useMemo(
        () => [
            {
                title: t('users.tabs.tenant.columnTenant'),
                dataIndex: 'tenantName',
                key: 'tenantName',
                width: 150,
                ellipsis: true,
                sorter: (a, b) => a.tenantName.localeCompare(b.tenantName),
            },
            {
                title: t('users.unified.columnType'),
                key: 'userType',
                width: 130,
                render: (_: unknown, row) => <UserTypeBadge row={row} />,
            },
            {
                title: t('users.list.columnName'),
                dataIndex: 'name',
                key: 'name',
            },
            {
                title: t('users.list.columnEmail'),
                dataIndex: 'email',
                key: 'email',
                ellipsis: true,
            },
            {
                title: t('users.list.columnRole'),
                dataIndex: 'role',
                key: 'role',
                width: 120,
                render: (role: string, row) => (
                    <UserRoleBadge
                        role={role}
                        isOwner={row.kind === 'tenant' ? row.isOwner : false}
                        platform={row.kind === 'platform'}
                    />
                ),
            },
            {
                title: t('users.list.columnStatus'),
                dataIndex: 'isActive',
                key: 'isActive',
                width: 100,
                render: (active: boolean) => (
                    <Tag color={active ? 'green' : 'red'}>
                        {active ? t('users.list.statusActive') : t('users.list.statusInactive')}
                    </Tag>
                ),
            },
            {
                title: t('users.list.columnLastLogin'),
                dataIndex: 'lastLoginAt',
                key: 'lastLoginAt',
                width: 180,
                render: (v: string | null | undefined) => (v ? formatDateTime(v, formatLocale) : '—'),
            },
            {
                title: t('users.list.columnActions'),
                key: 'actions',
                width: 150,
                render: (_: unknown, row) => {
                    if (row.kind === 'platform') {
                        const record = row.user;
                        return (
                            <Space wrap size="small">
                                {policy.canEdit && (
                                    <Button size="small" icon={<EyeOutlined />} onClick={() => onView(record)}>
                                        {t('users.list.view')}
                                    </Button>
                                )}
                                {policy.canEdit && (
                                    <Button
                                        size="small"
                                        icon={<EditOutlined />}
                                        onClick={() => onEdit(row.userId)}
                                    >
                                        {t('users.list.edit')}
                                    </Button>
                                )}
                                {policy.canDeactivate && record.isActive && (
                                    <Button
                                        size="small"
                                        danger
                                        icon={<StopOutlined />}
                                        onClick={() => onDeactivate(record)}
                                    >
                                        {t('users.tabs.platform.deactivateAccount')}
                                    </Button>
                                )}
                                {policy.canReactivate && !record.isActive && (
                                    <Button
                                        size="small"
                                        type="primary"
                                        icon={<CheckCircleOutlined />}
                                        onClick={() => onReactivate(record)}
                                    >
                                        {t('users.list.reactivate')}
                                    </Button>
                                )}
                                {policy.canResetPassword(record.role) && record.id !== currentUserId && (
                                    <Button
                                        size="small"
                                        icon={<KeyOutlined />}
                                        onClick={() => onResetPassword(record)}
                                    >
                                        {t('users.list.resetPassword')}
                                    </Button>
                                )}
                            </Space>
                        );
                    }

                    return (
                        <Space wrap size="small">
                            {policy.canEdit && (
                                <Button size="small" icon={<EditOutlined />} onClick={() => onEdit(row.userId)}>
                                    {t('users.list.edit')}
                                </Button>
                            )}
                            {policy.canProvisionTenantCredentials && policy.canResetPassword(row.role) && (
                                <Button size="small" icon={<KeyOutlined />} onClick={() => setResetRow(row.row)}>
                                    {t('users.list.resetPassword')}
                                </Button>
                            )}
                            {policy.canEdit && (
                                <Popconfirm
                                    title={t('users.tabs.tenant.confirmRemove.title')}
                                    description={t('users.tabs.tenant.confirmRemove.body', {
                                        tenant: row.tenantSlug,
                                    })}
                                    onConfirm={() =>
                                        removeMutation.mutate({
                                            tenantId: row.tenantId,
                                            userId: row.userId,
                                        })
                                    }
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
                            )}
                        </Space>
                    );
                },
            },
        ],
        [
            t,
            formatLocale,
            policy,
            currentUserId,
            onView,
            onEdit,
            onDeactivate,
            onReactivate,
            onResetPassword,
            removeMutation,
        ],
    );

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('users.unified.pageIntro')}
            </Typography.Paragraph>

            <Card size="small" title={t('users.list.filterCardTitle')}>
                <Flex wrap="wrap" gap="small" align="center">
                    <Typography.Text type="secondary">{t('users.unified.filterTenantLabel')}</Typography.Text>
                    <TenantFilter
                        value={selectedTenant}
                        onChange={handleTenantFilterChange}
                        includePlatformOption
                    />
                    <Input.Search
                        allowClear
                        placeholder={t('users.tabs.tenant.searchPlaceholder')}
                        style={{ width: 280 }}
                        value={searchInput}
                        onChange={(e) => {
                            const v = e.target.value;
                            setSearchInput(v);
                            if (!v) setSearch('');
                        }}
                        onSearch={(v) => setSearch(v ?? '')}
                        enterButton={<SearchOutlined />}
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
                    <Select
                        placeholder={t('users.list.filterStatusPlaceholder')}
                        allowClear
                        style={{ width: 120 }}
                        value={
                            statusFilter === undefined
                                ? undefined
                                : statusFilter
                                  ? 'active'
                                  : 'inactive'
                        }
                        onChange={(v) => setStatusFilter(v === undefined ? undefined : v === 'active')}
                        options={statusFilterOptions}
                    />
                    <Button icon={<ReloadOutlined />} onClick={refetchAll} loading={isFetching}>
                        {t('users.list.actionRefresh')}
                    </Button>
                </Flex>
            </Card>

            {isError ? (
                <Alert
                    type="error"
                    showIcon
                    message={t('users.list.errorLoad')}
                    action={
                        <Button size="small" onClick={refetchAll}>
                            {t('users.list.retry')}
                        </Button>
                    }
                />
            ) : null}

            <Space wrap>
                {policy.canProvisionTenantCredentials && tenantFilter !== FILTER_PLATFORM ? (
                    <>
                        <Button type="primary" icon={<MailOutlined />} onClick={() => setInviteOpen(true)}>
                            {t('users.invite.action')}
                        </Button>
                        <Button icon={<ThunderboltOutlined />} onClick={() => setQuickOpen(true)}>
                            {t('users.unified.quickUserAction')}
                        </Button>
                    </>
                ) : null}
                {policy.canCreate ? (
                    <Button icon={<UserOutlined />} onClick={onCreatePlatformUser}>
                        {t('users.page.createPlatformAdmin')}
                    </Button>
                ) : null}
            </Space>

            <Table
                rowKey="key"
                loading={isLoading}
                dataSource={filteredRows}
                columns={columns}
                pagination={{ pageSize: 20, showSizeChanger: true, pageSizeOptions: [10, 20, 50] }}
                locale={{
                    emptyText: (
                        <Empty
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                            description={t('users.unified.empty')}
                        />
                    ),
                }}
            />

            {policy.canProvisionTenantCredentials ? (
                <Collapse
                    items={[
                        {
                            key: 'invitations',
                            label: t('users.unified.invitationsSection'),
                            children: <UserInvitationsPanel />,
                        },
                    ]}
                />
            ) : null}

            {policy.canProvisionTenantCredentials ? (
                <>
                    <InviteUserModal
                        open={inviteOpen}
                        variant="usersPage"
                        tenantId={inviteFixedTenantId}
                        tenantRows={inviteTenants}
                        tenantsLoading={inviteTenantsLoading}
                        confirmLoading={createMutation.isPending}
                        onClose={() => setInviteOpen(false)}
                        onSubmit={(values) => createMutation.mutate(values)}
                    />
                    <UserCreatedSuccessModal
                        open={!!createResult}
                        result={createResult}
                        onClose={() => setCreateResult(null)}
                    />
                    <QuickUserModal
                        open={quickOpen}
                        variant="usersPage"
                        tenantId={inviteFixedTenantId}
                        tenantSlug={quickFixedTenant?.slug}
                        tenantName={quickFixedTenant?.name}
                        tenantRows={inviteTenants}
                        tenantsLoading={inviteTenantsLoading}
                        confirmLoading={quickMutation.isPending}
                        onClose={() => setQuickOpen(false)}
                        onSubmit={(values) => quickMutation.mutate(values)}
                    />
                    <QuickUserSuccessModal
                        open={!!quickResult}
                        result={quickResult}
                        role={quickRole}
                        tenantName={quickTenantCtx?.name ?? ''}
                        tenantSlug={quickTenantCtx?.slug ?? 'tenant'}
                        onClose={() => setQuickResult(null)}
                        onGenerateAnother={() => {
                            setQuickResult(null);
                            setQuickOpen(true);
                        }}
                    />
                    <ResetPasswordModal
                        open={!!resetRow}
                        tenantId={resetRow?.tenantId ?? ''}
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
                        onCompleted={() => tenantUsersQuery.refetch()}
                    />
                </>
            ) : null}
        </Space>
    );
}
