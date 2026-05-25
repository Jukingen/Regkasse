'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
    Alert,
    Avatar,
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
    Tooltip,
    Typography,
    message,
} from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import {
    CheckCircleOutlined,
    EditOutlined,
    EyeOutlined,
    ReloadOutlined,
    SearchOutlined,
    StopOutlined,
    ThunderboltOutlined,
    UserAddOutlined,
    UserDeleteOutlined,
    UserOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';

import { CreateUserModal } from '@/features/users/components/CreateUserModal';
import type { CreateUserFormValues } from '@/features/users/components/CreateUserModal';
import { QuickUserModal } from '@/features/super-admin/components/QuickUserModal';
import type { QuickUserFormValues } from '@/features/super-admin/components/QuickUserModal';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { createQuickUser, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { useCreateUser } from '@/features/users/hooks/useCreateUser';
import { UserTenantCreatePanel } from '@/features/users/components/UserTenantCreatePanel';
import { UserRoleBadge } from '@/features/users/components/UserRoleBadge';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import {
    adminUserToUserInfo,
    adminUsersQueryKeys,
    listAllAdminUsers,
    listPlatformUsers,
    listTenantUsers,
    removeUserFromTenant,
    tenantRowToTenantUser,
    type AdminUserDto,
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
import { useDebounce } from '@/hooks/useDebounce';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { getColorFromName } from '@/features/users/utils/avatarColor';
import type { UnifiedAdminUserRow, UnifiedAdminUserType } from '@/features/users/types/unifiedAdminUserRow';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';
import styles from '@/features/users/components/unifiedAdminUsersTable.module.css';

export type { UnifiedAdminUserRow } from '@/features/users/types/unifiedAdminUserRow';

const TENANT_ROLE_FILTER_VALUES = ['SuperAdmin', 'Manager', 'Cashier', 'Accountant', 'Waiter', 'Kitchen'] as const;
const FILTER_ALL = ADMIN_USERS_FILTER_ALL;
const FILTER_PLATFORM = ADMIN_USERS_FILTER_PLATFORM;
const TABLE_SCROLL_X = 1200;
const TABLE_SCROLL_Y = 'calc(100vh - 250px)';
const DEFAULT_PAGE_SIZE = 25;
const SEARCH_DEBOUNCE_MS = 300;

function platformDisplayName(user: UserInfo): string {
    const first = user.firstName ?? '';
    const last = user.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || user.userName || user.id || '—';
}

function avatarInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0] ?? ''}${parts[parts.length - 1][0] ?? ''}`.toUpperCase();
}

function resolveUserType(dto: AdminUserDto): UnifiedAdminUserType {
    if (dto.userType === 'Tenant') return 'Tenant';
    if (dto.userType === 'Platform') return 'Platform';
    if (dto.tenantId && dto.tenantName) return 'Tenant';
    return 'Platform';
}

function rowFromAdminDto(dto: AdminUserDto): UnifiedAdminUserRow {
    const user = adminUserToUserInfo(dto);
    const userType = resolveUserType(dto);
    const kind = userType === 'Tenant' ? 'tenant' : 'platform';
    const tenantId = dto.tenantId ?? undefined;
    const tenantName = dto.tenantName ?? '';
    const tenantSlug = dto.tenantSlug ?? '';
    const base: UnifiedAdminUserRow = {
        key: tenantId ? `tenant:${tenantId}:${dto.id}` : `platform:${dto.id}`,
        kind,
        userType,
        userId: dto.id,
        name: platformDisplayName(user),
        email: dto.email ?? dto.userName ?? '—',
        role: dto.role ?? '—',
        isActive: dto.isActive,
        lastLoginAt: dto.lastLoginAt ?? null,
        tenantId,
        tenantSlug,
        tenantName,
        user,
    };
    if (kind === 'tenant' && tenantId) {
        base.row = {
            userId: dto.id,
            email: base.email,
            name: base.name,
            role: base.role,
            isOwner: false,
            joinedAtUtc: dto.createdAt ?? new Date(0).toISOString(),
            tenantId,
            tenantSlug,
            tenantName,
            isActive: dto.isActive,
            lastLoginAt: dto.lastLoginAt ?? undefined,
        };
    }
    return base;
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

/** Single super-admin user list with tenant metadata, filters, and row actions. */
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
    const [searchInput, setSearchInput] = useState('');
    const debouncedSearch = useDebounce(searchInput, SEARCH_DEBOUNCE_MS);
    const searchParam = debouncedSearch.trim() || undefined;
    const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
    const [currentPage, setCurrentPage] = useState(1);
    const [createOpen, setCreateOpen] = useState(false);
    const [quickOpen, setQuickOpen] = useState(false);
    const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
    const [quickRole, setQuickRole] = useState('Manager');
    const [quickTenantCtx, setQuickTenantCtx] = useState<{ name: string; slug: string } | null>(null);
    const [resetRow, setResetRow] = useState<TenantUserRow | null>(null);

    useEffect(() => {
        setSelectedTenant(
            tenantFilterToUiValue(resolveAdminUsersTenantFilterFromSearchParams(searchParams)),
        );
    }, [searchParams]);

    useEffect(() => {
        setCurrentPage(1);
    }, [tenantFilter, roleFilter, statusFilter, debouncedSearch]);

    const createFixedTenantId =
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

    const { tenants: createTenants, isLoading: createTenantsLoading } = useTenantList();

    const quickFixedTenant = useMemo(
        () =>
            createFixedTenantId
                ? createTenants.find((row) => row.id === createFixedTenantId)
                : undefined,
        [createFixedTenantId, createTenants],
    );

    const allUsersQuery = useQuery({
        queryKey: adminUsersQueryKeys.all(statusFilter, roleFilter, searchParam),
        queryFn: () =>
            listAllAdminUsers({
                ...(statusFilter != null ? { isActive: statusFilter } : {}),
                ...(roleFilter ? { role: roleFilter } : {}),
                ...(searchParam ? { search: searchParam } : {}),
            }),
        enabled: tenantFilter === FILTER_ALL,
        select: (data) => data.map(rowFromAdminDto),
    });

    const platformQuery = useQuery({
        queryKey: adminUsersQueryKeys.platform(statusFilter, searchParam),
        queryFn: () =>
            listPlatformUsers({
                ...(statusFilter != null ? { isActive: statusFilter } : {}),
                ...(searchParam ? { search: searchParam } : {}),
            }),
        enabled: tenantFilter === FILTER_PLATFORM,
        select: (data) => data.map(rowFromAdminDto),
    });

    const tenantApiTenantId =
        tenantFilter && tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM
            ? tenantFilter
            : undefined;

    const tenantUsersQuery = useQuery({
        queryKey: adminUsersQueryKeys.tenant(tenantApiTenantId, roleFilter, searchParam),
        queryFn: () =>
            listTenantUsers({
                tenantId: tenantApiTenantId,
                role: roleFilter || undefined,
                isActive: statusFilter,
                ...(searchParam ? { search: searchParam } : {}),
            }),
        enabled: tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM,
        select: (data) =>
            data.map((row): UnifiedAdminUserRow => {
                const tenantUser = tenantRowToTenantUser(row);
                return {
                    kind: 'tenant',
                    key: `tenant:${row.tenantId}:${row.userId}`,
                    userType: 'Tenant',
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
                    row: tenantUser,
                    user: {
                        id: row.userId,
                        email: row.email,
                        firstName: row.name.split(' ')[0] ?? '',
                        lastName: row.name.split(' ').slice(1).join(' '),
                        role: row.role,
                        isActive: row.isActive,
                        lastLoginAt: row.lastLoginAt ?? undefined,
                    },
                };
            }),
    });

    const statusFilterOptions = useMemo(
        () => [
            { value: 'active', label: t('users.unified.badges.active') },
            { value: 'inactive', label: t('users.unified.badges.inactive') },
        ],
        [t],
    );

    const unifiedRows = useMemo((): UnifiedAdminUserRow[] => {
        if (tenantFilter === FILTER_ALL) return allUsersQuery.data ?? [];
        if (tenantFilter === FILTER_PLATFORM) return platformQuery.data ?? [];
        return tenantUsersQuery.data ?? [];
    }, [allUsersQuery.data, platformQuery.data, tenantUsersQuery.data, tenantFilter]);

    const filteredRows = useMemo(() => {
        let rows = unifiedRows;
        if (tenantFilter === FILTER_PLATFORM) {
            rows = rows.filter((row) => row.userType === 'Platform');
        } else if (tenantApiTenantId) {
            rows = rows.filter((row) => row.tenantId === tenantApiTenantId);
        }
        if (roleFilter) {
            rows = rows.filter((row) => row.role === roleFilter);
        }
        return rows;
    }, [unifiedRows, roleFilter, tenantFilter, tenantApiTenantId]);

    const invalidateUserLists = () => {
        void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    };

    const createMutation = useCreateUser({
        fixedTenantId: createFixedTenantId,
        onSuccess: () => {
            setCreateOpen(false);
            invalidateUserLists();
        },
    });

    const quickMutation = useMutation({
        mutationFn: (values: QuickUserFormValues) => {
            const tenantId = values.tenantId ?? createFixedTenantId;
            if (!tenantId) throw new Error('tenantId required');
            return createQuickUser(tenantId, { role: values.role });
        },
        onSuccess: (res, values) => {
            const tenantId = values.tenantId ?? createFixedTenantId;
            const tenant = createTenants.find((row) => row.id === tenantId);
            setQuickTenantCtx({
                name: tenant?.name ?? tenantId ?? '',
                slug: tenant?.slug ?? 'tenant',
            });
            setQuickOpen(false);
            setQuickRole(values.role);
            setQuickResult(res);
            invalidateUserLists();
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
            invalidateUserLists();
        },
        onError: () => message.error(t('users.list.errorLoad')),
    });

    const isLoading =
        (tenantFilter === FILTER_ALL && allUsersQuery.isLoading) ||
        (tenantFilter === FILTER_PLATFORM && platformQuery.isLoading) ||
        (tenantFilter !== FILTER_ALL &&
            tenantFilter !== FILTER_PLATFORM &&
            tenantUsersQuery.isLoading);

    const isFetching = allUsersQuery.isFetching || platformQuery.isFetching || tenantUsersQuery.isFetching;
    const isError = allUsersQuery.isError || platformQuery.isError || tenantUsersQuery.isError;

    const refetchAll = () => {
        void allUsersQuery.refetch();
        void platformQuery.refetch();
        void tenantUsersQuery.refetch();
    };

    const renderTenantCell = (row: UnifiedAdminUserRow) => {
        if (row.userType === 'Platform') {
            return <Tag color="purple">Plattform</Tag>;
        }
        if (!row.tenantName) {
            return <Tag color="default">—</Tag>;
        }
        return <span>{row.tenantName}</span>;
    };

    const renderStatusBadge = (row: UnifiedAdminUserRow) => {
        if (row.isPending) {
            return <Tag color="orange">{t('users.unified.badges.pending')}</Tag>;
        }
        return (
            <Tag color={row.isActive ? 'green' : 'red'}>
                {row.isActive ? t('users.unified.badges.active') : t('users.unified.badges.inactive')}
            </Tag>
        );
    };

    const renderTwoFactorBadge = () => (
        <Tag color="default">{t('users.unified.twoFactorNo')}</Tag>
    );

    const renderActions = (row: UnifiedAdminUserRow) => {
        const record = row.user;
        const isPlatformRow = row.userType === 'Platform';

        return (
            <Space size={4} wrap>
                {policy.canEdit && isPlatformRow ? (
                    <Tooltip title={t('users.list.view')}>
                        <Button
                            type="text"
                            size="small"
                            icon={<EyeOutlined />}
                            aria-label={t('users.list.view')}
                            onClick={() => onView(record)}
                        />
                    </Tooltip>
                ) : null}
                {policy.canEdit ? (
                    <Tooltip title={t('users.list.edit')}>
                        <Button
                            type="text"
                            size="small"
                            icon={<EditOutlined />}
                            aria-label={t('users.list.edit')}
                            onClick={() => onEdit(row.userId)}
                        />
                    </Tooltip>
                ) : null}
                {policy.canResetPassword(row.role) && record.id !== currentUserId ? (
                    <Tooltip title={t('users.list.resetPassword')}>
                        <Button
                            type="text"
                            size="small"
                            icon={<ReloadOutlined />}
                            aria-label={t('users.list.resetPassword')}
                            onClick={() => {
                                if (row.row) {
                                    setResetRow(row.row);
                                    return;
                                }
                                onResetPassword(record);
                            }}
                        />
                    </Tooltip>
                ) : null}
                {policy.canDeactivate && record.isActive && isPlatformRow ? (
                    <Tooltip title={t('users.tabs.platform.deactivateAccount')}>
                        <Button
                            type="text"
                            size="small"
                            danger
                            icon={<StopOutlined />}
                            aria-label={t('users.tabs.platform.deactivateAccount')}
                            onClick={() => onDeactivate(record)}
                        />
                    </Tooltip>
                ) : null}
                {policy.canReactivate && !record.isActive && isPlatformRow ? (
                    <Tooltip title={t('users.list.reactivate')}>
                        <Button
                            type="text"
                            size="small"
                            icon={<CheckCircleOutlined />}
                            aria-label={t('users.list.reactivate')}
                            onClick={() => onReactivate(record)}
                        />
                    </Tooltip>
                ) : null}
                {row.kind === 'tenant' && row.tenantId && policy.canEdit ? (
                    <Popconfirm
                        title={t('users.tabs.tenant.confirmRemove.title')}
                        description={t('users.tabs.tenant.confirmRemove.body', {
                            tenant: row.tenantSlug || row.tenantName,
                        })}
                        onConfirm={() =>
                            removeMutation.mutate({
                                tenantId: row.tenantId!,
                                userId: row.userId,
                            })
                        }
                    >
                        <Tooltip title={t('users.tabs.tenant.removeFromTenant')}>
                            <Button
                                type="text"
                                size="small"
                                danger
                                icon={<UserDeleteOutlined />}
                                loading={removeMutation.isPending}
                                aria-label={t('users.tabs.tenant.removeFromTenant')}
                            />
                        </Tooltip>
                    </Popconfirm>
                ) : null}
            </Space>
        );
    };

    const columns: ColumnsType<UnifiedAdminUserRow> = useMemo(
        () => [
            {
                title: t('users.unified.columns.user'),
                key: 'user',
                width: '25%',
                render: (_: unknown, row) => (
                    <Flex gap="small" align="center">
                        <Avatar style={{ backgroundColor: getColorFromName(row.name) }}>
                            {avatarInitials(row.name)}
                        </Avatar>
                        <Flex vertical gap={0} style={{ minWidth: 0, flex: 1 }}>
                            <Typography.Text strong ellipsis>
                                {row.name}
                            </Typography.Text>
                            <Typography.Text type="secondary" ellipsis style={{ fontSize: 12 }}>
                                {row.email}
                            </Typography.Text>
                        </Flex>
                    </Flex>
                ),
            },
            {
                title: t('users.unified.columns.tenant'),
                key: 'tenant',
                width: '15%',
                ellipsis: true,
                render: (_: unknown, row) => renderTenantCell(row),
                sorter: (a, b) => (a.tenantName || '').localeCompare(b.tenantName || ''),
            },
            {
                title: t('users.unified.columns.role'),
                dataIndex: 'role',
                key: 'role',
                width: '12%',
                render: (role: string, row) => (
                    <UserRoleBadge
                        role={role}
                        isOwner={row.kind === 'tenant' ? row.isOwner : false}
                    />
                ),
            },
            {
                title: t('users.unified.columns.status'),
                key: 'status',
                width: '10%',
                render: (_: unknown, row) => renderStatusBadge(row),
            },
            {
                title: t('users.unified.columns.lastLogin'),
                dataIndex: 'lastLoginAt',
                key: 'lastLoginAt',
                width: '12%',
                render: (v: string | null | undefined) =>
                    v ? formatDateTime(v, formatLocale) : '—',
            },
            {
                title: t('users.unified.columns.twoFactor'),
                key: 'twoFactor',
                width: '8%',
                align: 'center',
                render: () => renderTwoFactorBadge(),
            },
            {
                title: t('users.unified.columns.actions'),
                key: 'actions',
                width: '18%',
                fixed: 'right',
                render: (_: unknown, row) => renderActions(row),
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
            removeMutation.isPending,
        ],
    );

    const pagination: TablePaginationConfig = {
        current: currentPage,
        pageSize,
        total: filteredRows.length,
        showSizeChanger: true,
        pageSizeOptions: [10, 25, 50, 100],
        showTotal: (total, range) =>
            t('users.list.paginationRange', { from: range[0], to: range[1], total }),
        onChange: (page, size) => {
            setCurrentPage(page);
            if (size && size !== pageSize) setPageSize(size);
        },
    };

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('users.unified.pageIntro')}
            </Typography.Paragraph>

            <Card size="small" styles={{ body: { padding: 16 } }}>
                <Flex wrap="wrap" gap="middle" align="center">
                    <Input
                        allowClear
                        prefix={<SearchOutlined />}
                        placeholder={t('users.unified.filters.searchPlaceholder')}
                        style={{ width: 280, flex: '1 1 220px', maxWidth: 360 }}
                        value={searchInput}
                        onChange={(e) => setSearchInput(e.target.value)}
                    />
                    <TenantFilter
                        value={selectedTenant}
                        onChange={handleTenantFilterChange}
                        includePlatformOption
                        style={{ minWidth: 220, flex: '1 1 200px' }}
                    />
                    <Select
                        allowClear
                        placeholder={t('users.unified.filters.roleFilter')}
                        style={{ minWidth: 160 }}
                        value={roleFilter}
                        onChange={setRoleFilter}
                        options={TENANT_ROLE_FILTER_VALUES.map((role) => ({
                            value: role,
                            label: roleDisplayLabel(role),
                        }))}
                    />
                    <Select
                        placeholder={t('users.unified.filters.statusFilter')}
                        allowClear
                        style={{ width: 140 }}
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

            <Flex wrap="wrap" gap="small">
                {policy.canProvisionTenantCredentials && tenantFilter !== FILTER_PLATFORM ? (
                    <>
                        <Button type="primary" icon={<UserAddOutlined />} onClick={() => setCreateOpen(true)}>
                            {t('users.create.action')}
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
            </Flex>

            <div className={styles.tableWrap}>
                <Table<UnifiedAdminUserRow>
                    rowKey="key"
                    sticky
                    loading={isLoading || (isFetching && !!searchParam)}
                    dataSource={filteredRows}
                    columns={columns}
                    scroll={{ x: TABLE_SCROLL_X, y: TABLE_SCROLL_Y }}
                    pagination={pagination}
                locale={{
                    emptyText: (
                        <Empty
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                            description={t('users.unified.empty')}
                        />
                    ),
                }}
                />
            </div>

            {policy.canProvisionTenantCredentials ? (
                <Collapse
                    items={[
                        {
                            key: 'tenant-create',
                            label: t('users.unified.tenantCreateSection'),
                            children: <UserTenantCreatePanel />,
                        },
                    ]}
                />
            ) : null}

            {policy.canProvisionTenantCredentials ? (
                <>
                    <CreateUserModal
                        open={createOpen}
                        variant="usersPage"
                        isSuperAdmin
                        tenantId={createFixedTenantId}
                        tenantRows={createTenants}
                        tenantsLoading={createTenantsLoading}
                        confirmLoading={createMutation.isPending}
                        onClose={() => setCreateOpen(false)}
                        onComplete={() => setCreateOpen(false)}
                        onSubmit={(values) => createMutation.mutateAsync(values)}
                    />
                    <QuickUserModal
                        open={quickOpen}
                        variant="usersPage"
                        tenantId={createFixedTenantId}
                        tenantSlug={quickFixedTenant?.slug}
                        tenantName={quickFixedTenant?.name}
                        tenantRows={createTenants}
                        tenantsLoading={createTenantsLoading}
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
