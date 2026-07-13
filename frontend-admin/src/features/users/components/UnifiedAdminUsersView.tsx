'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useEffect, useMemo, useState } from 'react';
import { Alert, Avatar, Badge, Button, Card, Collapse, Dropdown, Empty, Flex, Input, Popconfirm, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { MenuProps } from 'antd';
import dayjs from 'dayjs';
import {
    SafetyOutlined,
    CheckCircleOutlined,
    DownloadOutlined,
    EditOutlined,
    EyeOutlined,
    FileExcelOutlined,
    ImportOutlined,
    FilePdfOutlined,
    GlobalOutlined,
    ReloadOutlined,
    ShopOutlined,
    StopOutlined,
    UserAddOutlined,
    UserDeleteOutlined,
    UserOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';

import { BulkImportModal } from '@/features/users/components/BulkImportModal';
import { CreateUserModal } from '@/features/users/components/CreateUserModal';
import { EditUsernameModal } from '@/features/users/components/EditUsernameModal';
import type { CreateUserQuickFormValues } from '@/features/users/components/CreateUserModal';
import { createQuickUser, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { UserRoleBadge } from '@/features/users/components/UserRoleBadge';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import { PasswordViewModal } from '@/features/users/components/PasswordViewModal';
import {
    adminUserToUserInfo,
    adminUsersQueryKeys,
    createUser as createAdminUser,
    createPlatformUser,
    listAllAdminUsers,
    listPlatformUsers,
    listTenantUsers,
    removeUserFromTenant,
    updateUserTenants,
    type AdminUserDto,
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
import { useDebounce } from '@/hooks/useDebounce';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { getColorFromEmail } from '@/features/users/utils/avatarColor';
import type { UnifiedAdminUserRow, UnifiedAdminUserType } from '@/features/users/types/unifiedAdminUserRow';
import { isPlatformUserRole } from '@/features/users/utils/userScope';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';
import styles from '@/features/users/components/unifiedAdminUsersTable.module.css';

export type { UnifiedAdminUserRow } from '@/features/users/types/unifiedAdminUserRow';

const TENANT_ROLE_FILTER_VALUES = ['SuperAdmin', 'Manager', 'Cashier', 'Accountant', 'Waiter', 'Kitchen'] as const;
const FILTER_ALL = ADMIN_USERS_FILTER_ALL;
const FILTER_PLATFORM = ADMIN_USERS_FILTER_PLATFORM;
const TABLE_SCROLL_X = 1520;
const TABLE_SCROLL_Y = 'calc(100vh - 250px)';
const SEARCH_DEBOUNCE_MS = 300;
const TEMP_PASSWORD_MASK = '***';
const PLATFORM_GROUP_KEY = '__platform__';

function generateQuickPlatformEmail(role: string): string {
    const normalizedRole = role.trim().toLowerCase().replace(/[^a-z0-9]+/g, '') || 'user';
    const random = Math.random().toString(36).slice(2, 8);
    return `${normalizedRole}_${random}@platform.regkasse.at`;
}

function platformDisplayName(user: UserInfo): string {
    const first = user.firstName ?? '';
    const last = user.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || user.userName || user.id || '—';
}

function avatarLabel(row: UnifiedAdminUserRow): string {
    return row.name?.trim() || '—';
}

function avatarCharacter(row: UnifiedAdminUserRow): string {
    const displayName = avatarLabel(row);
    if (displayName !== '—') {
        return displayName.charAt(0).toUpperCase();
    }
    const email = row.email?.trim();
    if (email) {
        return email.charAt(0).toUpperCase();
    }
    return '?';
}

function escapeCsvValue(value: string): string {
    return `"${value.replace(/"/g, '""')}"`;
}

function escapeHtml(value: string): string {
    return value
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
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
    const userId = dto.id ?? '';
    const base: UnifiedAdminUserRow = {
        key: tenantId ? `tenant:${tenantId}:${userId}` : `platform:${userId}`,
        kind,
        userType,
        userId,
        name: platformDisplayName(user),
        email: dto.email ?? dto.userName ?? '—',
        role: dto.role ?? '—',
        isActive: dto.isActive ?? false,
        lastLoginAt: dto.lastLoginAt ?? null,
        twoFactorEnabled: dto.twoFactorEnabled,
        tenantId,
        tenantSlug,
        tenantName,
        user,
    };
    if (kind === 'tenant' && tenantId) {
        base.row = {
            userId,
            userName: dto.userName?.trim() || user.userName?.trim() || userId,
            email: base.email,
            name: base.name,
            role: base.role,
            isOwner: false,
            joinedAtUtc: dto.createdAt ?? new Date(0).toISOString(),
            tenantId,
            tenantSlug,
            tenantName,
            isActive: dto.isActive ?? false,
            lastLoginAt: dto.lastLoginAt ?? undefined,
            twoFactorEnabled: dto.twoFactorEnabled,
        };
    }
    return base;
}

export type UnifiedAdminUsersViewProps = {
    policy: UsersPolicy;
    roleDisplayLabel: (role: string) => string;
    currentUserId?: string | null;
    /** When set, locks list/create to one mandant (Manager JWT context). Hides platform scope. */
    tenantScopeId?: string;
    /** False for tenant Managers — hides platform filter, groups, and platform-user actions. */
    isSuperAdminActor?: boolean;
    onView: (user: UserInfo) => void;
    onEdit: (userId: string) => void;
    onDeactivate: (user: UserInfo) => void;
    onReactivate: (user: UserInfo) => void;
    onResetPassword: (user: UserInfo) => void;
    onManagePermissions?: (user: UserInfo) => void;
    onCreatePlatformUser: () => void;
};

/** Single super-admin user list with tenant metadata, filters, and row actions. */
export function UnifiedAdminUsersView({
    policy,
    roleDisplayLabel,
    currentUserId,
    tenantScopeId,
    isSuperAdminActor = true,
    onView,
    onEdit,
    onDeactivate,
    onReactivate,
    onResetPassword,
    onManagePermissions,
    onCreatePlatformUser,
}: UnifiedAdminUsersViewProps) {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const router = useRouter();
    const pathname = usePathname();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();

    const isTenantScoped = Boolean(tenantScopeId);
    const [selectedTenant, setSelectedTenant] = useState(() =>
        tenantScopeId ?? tenantFilterToUiValue(resolveAdminUsersTenantFilterFromSearchParams(searchParams)),
    );
    const tenantFilter = isTenantScoped
        ? tenantScopeId!
        : tenantFilterFromUiValue(selectedTenant);
    const [roleFilter, setRoleFilter] = useState<string | undefined>();
    const [statusFilter, setStatusFilter] = useState<boolean | undefined>(true);
    const [searchInput, setSearchInput] = useState('');
    const debouncedSearch = useDebounce(searchInput, SEARCH_DEBOUNCE_MS);
    const searchParam = debouncedSearch.trim() || undefined;
    const [createOpen, setCreateOpen] = useState(false);
    const [bulkImportOpen, setBulkImportOpen] = useState(false);
    const [resetRow, setResetRow] = useState<TenantUserRow | null>(null);
    const [passwordRow, setPasswordRow] = useState<UnifiedAdminUserRow | null>(null);
    const [usernameEditUser, setUsernameEditUser] = useState<UserInfo | null>(null);

    useEffect(() => {
        setCreateOpen(false);
        return () => setCreateOpen(false);
    }, [pathname]);

    useEffect(() => {
        if (tenantScopeId) {
            setSelectedTenant(tenantScopeId);
            return;
        }
        setSelectedTenant(
            tenantFilterToUiValue(resolveAdminUsersTenantFilterFromSearchParams(searchParams)),
        );
    }, [searchParams, tenantScopeId]);

    const createFixedTenantId = tenantScopeId
        ? tenantScopeId
        : tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM
          ? tenantFilter
          : undefined;

    const canCreateTenantUsers =
        policy.canCreate &&
        (policy.canProvisionTenantCredentials || isTenantScoped) &&
        tenantFilter !== FILTER_PLATFORM;

    const handleTenantFilterChange = (value: string) => {
        if (isTenantScoped) return;
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

    const handleSearch = (value: string) => {
        setSearchInput(value);
    };

    const handleResetFilters = () => {
        setSearchInput('');
        setRoleFilter(undefined);
        setStatusFilter(true);
        if (!isTenantScoped) {
            handleTenantFilterChange(tenantFilterToUiValue(FILTER_ALL));
        }
    };

    const { tenants: createTenants, isLoading: createTenantsLoading } = useTenantList();

    const allUsersQuery = useQuery({
        queryKey: adminUsersQueryKeys.all(statusFilter, roleFilter, searchParam),
        queryFn: () =>
            listAllAdminUsers({
                ...(statusFilter != null ? { isActive: statusFilter } : {}),
                ...(roleFilter ? { role: roleFilter } : {}),
                ...(searchParam ? { search: searchParam } : {}),
            }),
        enabled: !isTenantScoped && tenantFilter === FILTER_ALL,
        select: (data) => data.map(rowFromAdminDto),
    });

    const platformQuery = useQuery({
        queryKey: adminUsersQueryKeys.platform(statusFilter, searchParam),
        queryFn: () =>
            listPlatformUsers({
                ...(statusFilter != null ? { isActive: statusFilter } : {}),
                ...(searchParam ? { search: searchParam } : {}),
            }),
        enabled: isSuperAdminActor && !isTenantScoped && tenantFilter === FILTER_PLATFORM,
        select: (data) => data.map(rowFromAdminDto),
    });

    const tenantApiTenantId = isTenantScoped
        ? tenantScopeId
        : tenantFilter && tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM
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
        enabled: isTenantScoped || (tenantFilter !== FILTER_ALL && tenantFilter !== FILTER_PLATFORM),
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
                    twoFactorEnabled: row.twoFactorEnabled,
                    row: tenantUser,
                    user: {
                        id: row.userId,
                        userName: row.userName || undefined,
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
        if (isTenantScoped || !isSuperAdminActor) {
            rows = rows.filter((row) => row.userType !== 'Platform');
        }
        if (tenantFilter === FILTER_PLATFORM) {
            rows = rows.filter((row) => row.userType === 'Platform');
        } else if (tenantApiTenantId) {
            rows = rows.filter((row) => row.tenantId === tenantApiTenantId);
        }
        if (roleFilter) {
            rows = rows.filter((row) => row.role === roleFilter);
        }
        return rows;
    }, [unifiedRows, roleFilter, tenantFilter, tenantApiTenantId, isTenantScoped, isSuperAdminActor]);

    const hasCustomFilters =
        (!isTenantScoped && tenantFilter !== FILTER_ALL) ||
        Boolean(roleFilter) ||
        statusFilter !== true ||
        searchInput.trim().length > 0;

    const groupedRows = useMemo(() => {
        const groups = new Map<
            string,
            {
                key: string;
                kind: 'platform' | 'tenant';
                title: string;
                rows: UnifiedAdminUserRow[];
            }
        >();

        filteredRows.forEach((row) => {
            const isPlatform = row.userType === 'Platform';
            const groupKey = isPlatform ? PLATFORM_GROUP_KEY : row.tenantId || row.tenantSlug || row.tenantName || 'unknown';

            if (!groups.has(groupKey)) {
                groups.set(groupKey, {
                    key: groupKey,
                    kind: isPlatform ? 'platform' : 'tenant',
                    title: isPlatform
                        ? t('users.tabs.labelPlatform')
                        : row.tenantName?.trim() || row.tenantSlug?.trim() || '—',
                    rows: [],
                });
            }

            groups.get(groupKey)?.rows.push(row);
        });

        return Array.from(groups.values()).sort((a, b) => {
            if (a.kind !== b.kind) return a.kind === 'platform' ? -1 : 1;
            return a.title.localeCompare(b.title);
        });
    }, [filteredRows, t]);

    const invalidateUserLists = () => {
        void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    };

    const createMutation = useMutation({
        mutationFn: (values: {
            email: string;
            firstName?: string;
            lastName?: string;
            role: string;
            tenantId?: string;
            isOwner?: boolean;
        }) => {
            const tenantId = tenantScopeId ?? values.tenantId ?? createFixedTenantId;
            if (tenantId) {
                return createAdminUser({
                    email: values.email.trim(),
                    firstName: values.firstName?.trim(),
                    lastName: values.lastName?.trim(),
                    role: values.role,
                    tenantId,
                    isOwner: values.isOwner,
                });
            }
            if (!isSuperAdminActor) {
                throw new Error('tenantId required for tenant-scoped user create');
            }
            return createPlatformUser({
                email: values.email.trim(),
                firstName: values.firstName?.trim(),
                lastName: values.lastName?.trim(),
                role: values.role,
            });
        },
        onSuccess: () => {
            invalidateUserLists();
        },
        onError: () => {
            message.error(t('tenants.users.create.messages.failed'));
        },
    });
    const assignTenantsMutation = useMutation({
        mutationFn: ({ userId, tenantIds }: { userId: string; tenantIds: string[] }) =>
            updateUserTenants(userId, tenantIds),
        onSuccess: () => {
            invalidateUserLists();
        },
        onError: () => {
            message.error(t('users.tenants.manageFailed'));
        },
    });

    const quickMutation = useMutation({
        mutationFn: (values: CreateUserQuickFormValues) => {
            const tenantId = tenantScopeId ?? values.tenantId ?? createFixedTenantId;
            if (!tenantId) throw new Error('tenantId required');
            return createQuickUser(tenantId, { role: values.role });
        },
        onSuccess: (_res: CreateQuickUserResult) => {
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
    const quickPlatformMutation = useMutation({
        mutationFn: (values: CreateUserQuickFormValues) =>
            createPlatformUser({
                email: generateQuickPlatformEmail(values.role),
                role: values.role,
            }),
        onSuccess: () => {
            invalidateUserLists();
        },
        onError: () => {
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
            return (
                <Tag icon={<GlobalOutlined />} color="purple">
                    Plattform
                </Tag>
            );
        }
        const tenantLabel = row.tenantName?.trim() || row.tenantSlug?.trim();
        if (!tenantLabel) {
            return <Tag color="default">—</Tag>;
        }
        return (
            <Space size={[4, 4]} wrap>
                <Tag icon={<ShopOutlined />} color="blue">
                    {tenantLabel}
                </Tag>
                {row.isOwner ? <Tag color="gold">{t('users.tabs.tenant.ownerBadge')}</Tag> : null}
            </Space>
        );
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

    const renderLastLoginCell = (value: string | null | undefined) => {
        if (!value) {
            return t('users.unified.lastLogin.never');
        }
        return formatDateTime(value, formatLocale);
    };

    const renderTwoFactorBadge = (enabled?: boolean) => (
        <Tag color={enabled ? 'green' : 'default'} icon={<SafetyOutlined />}>
            {t(enabled ? 'users.unified.twoFactorYes' : 'users.unified.twoFactorNo')}
        </Tag>
    );

    const renderPasswordCell = (row: UnifiedAdminUserRow) => {
        if (!isSuperAdminActor) {
            return null;
        }

        const canRevealPassword =
            policy.canResetPassword(row.role) &&
            row.userId !== currentUserId &&
            policy.canProvisionTenantCredentials;

        return (
            <Space size="small">
                <Typography.Text type="secondary">{TEMP_PASSWORD_MASK}</Typography.Text>
                {canRevealPassword ? (
                    <Tooltip title={t('users.password.showButtonTitle')}>
                        <Button
                            type="text"
                            size="small"
                            icon={<EyeOutlined />}
                            aria-label={t('users.password.showButtonTitle')}
                            onClick={() => setPasswordRow(row)}
                        />
                    </Tooltip>
                ) : null}
            </Space>
        );
    };

    const renderActions = (row: UnifiedAdminUserRow) => {
        const record = row.user;
        const isPlatformRow = row.userType === 'Platform';
        const canDeactivateUser = policy.canDeactivate && record.isActive && !isPlatformUserRole(row.role);
        const canReactivateUser = policy.canReactivate && !record.isActive;
        const actions: React.ReactNode[] = [];

        if (policy.canEdit && isPlatformRow) {
            actions.push(
                <Tooltip key="view" title={t('users.list.view')}>
                    <Button
                        size="small"
                        icon={<EyeOutlined />}
                        aria-label={t('users.list.view')}
                        onClick={() => onView(record)}
                    />
                </Tooltip>,
            );
        }

        if (row.userId) {
            actions.push(
                <Link key="details" href={`/admin/users/${row.userId}`}>
                    <Button type="link" size="small">
                        {t('users.list.details')}
                    </Button>
                </Link>,
            );
        }

        if (policy.canEdit) {
            actions.push(
                <Tooltip key="edit" title={t('users.list.edit')}>
                    <Button
                        size="small"
                        icon={<EditOutlined />}
                        aria-label={t('users.list.edit')}
                        onClick={() => onEdit(row.userId)}
                    />
                </Tooltip>,
            );
        }

        if (policy.canEdit && record.id) {
            actions.push(
                <Button
                    key="edit-username"
                    type="link"
                    size="small"
                    onClick={() => setUsernameEditUser(record)}
                >
                    {t('users.username.editTitle')}
                </Button>,
            );
        }

        if (
            policy.canManagePermissions &&
            onManagePermissions &&
            record.id &&
            !isPlatformUserRole(row.role)
        ) {
            actions.push(
                <Tooltip key="permissions" title={t('users.permissionsModal.action')}>
                    <Button
                        size="small"
                        icon={<SafetyOutlined />}
                        aria-label={t('users.permissionsModal.action')}
                        onClick={() => onManagePermissions(record)}
                    />
                </Tooltip>,
            );
        }

        if (policy.canResetPassword(row.role) && record.id !== currentUserId) {
            actions.push(
                <Tooltip key="reset-password" title={t('users.list.resetPassword')}>
                    <Button
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
                </Tooltip>,
            );
        }

        if (canDeactivateUser) {
            actions.push(
                <Tooltip key="deactivate" title={t('users.tabs.platform.deactivateAccount')}>
                    <Button
                        size="small"
                        danger
                        icon={<StopOutlined />}
                        aria-label={t('users.tabs.platform.deactivateAccount')}
                        onClick={() => onDeactivate(record)}
                    />
                </Tooltip>,
            );
        }

        if (canReactivateUser) {
            actions.push(
                <Tooltip key="reactivate" title={t('users.list.reactivate')}>
                    <Button
                        size="small"
                        icon={<CheckCircleOutlined />}
                        aria-label={t('users.list.reactivate')}
                        onClick={() => onReactivate(record)}
                    />
                </Tooltip>,
            );
        }

        if (row.kind === 'tenant' && row.tenantId && policy.canEdit) {
            actions.push(
                <Popconfirm
                    key="remove-from-tenant"
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
                            size="small"
                            danger
                            icon={<UserDeleteOutlined />}
                            loading={removeMutation.isPending}
                            aria-label={t('users.tabs.tenant.removeFromTenant')}
                        />
                    </Tooltip>
                </Popconfirm>,
            );
        }

        if (actions.length === 0) {
            return <Typography.Text type="secondary">—</Typography.Text>;
        }

        return <Space size={4} wrap>{actions}</Space>;
    };

    const getTenantExportLabel = (row: UnifiedAdminUserRow) =>
        row.userType === 'Platform'
            ? t('users.unified.filters.platform')
            : row.tenantName?.trim() || row.tenantSlug?.trim() || '—';

    const getStatusExportLabel = (row: UnifiedAdminUserRow) => {
        if (row.isPending) return t('users.unified.badges.pending');
        return row.isActive ? t('users.unified.badges.active') : t('users.unified.badges.inactive');
    };

    const getLastLoginExportLabel = (value: string | null | undefined) =>
        value ? formatDateTime(value, formatLocale) : t('users.unified.lastLogin.never');

    const downloadTextFile = (filename: string, content: string, type: string) => {
        const blob = new Blob([content], { type });
        const url = globalThis.URL.createObjectURL(blob);
        const a = globalThis.document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        globalThis.URL.revokeObjectURL(url);
    };

    const handleExportCsv = () => {
        try {
            const headers = [
                t('users.unified.columns.user'),
                t('users.unified.columns.userName'),
                t('users.unified.columns.tenant'),
                t('users.unified.columns.role'),
                t('users.unified.columns.status'),
                t('users.unified.columns.lastLogin'),
                t('users.unified.columns.twoFactor'),
            ];
            const rows = filteredRows.map((row) => [
                avatarLabel(row),
                row.user.userName?.trim() || '—',
                getTenantExportLabel(row),
                row.role,
                getStatusExportLabel(row),
                getLastLoginExportLabel(row.lastLoginAt),
                t(row.twoFactorEnabled ? 'users.unified.twoFactorYes' : 'users.unified.twoFactorNo'),
            ]);
            const csv = [headers, ...rows]
                .map((line) => line.map((value) => escapeCsvValue(String(value ?? ''))).join(';'))
                .join('\r\n');

            downloadTextFile(
                `admin-users-${dayjs().format('YYYYMMDD_HHmmss')}.csv`,
                `\uFEFF${csv}`,
                'text/csv;charset=utf-8;',
            );
            message.success(t('users.unified.export.messages.csvSuccess'));
        } catch {
            message.error(t('users.unified.export.messages.csvFailed'));
        }
    };

    const handleExportPdf = () => {
        const popup = globalThis.window.open('', '_blank', 'noopener,noreferrer');
        if (!popup) {
            message.error(t('users.unified.export.messages.pdfBlocked'));
            return;
        }

        const sections = groupedRows
            .map((group) => {
                const tableRows = group.rows
                    .map(
                        (row) => `
                            <tr>
                                <td>${escapeHtml(avatarLabel(row))}</td>
                                <td>${escapeHtml(row.user.userName?.trim() || '—')}</td>
                                <td>${escapeHtml(getTenantExportLabel(row))}</td>
                                <td>${escapeHtml(row.role)}</td>
                                <td>${escapeHtml(getStatusExportLabel(row))}</td>
                                <td>${escapeHtml(getLastLoginExportLabel(row.lastLoginAt))}</td>
                                <td>${escapeHtml(t(row.twoFactorEnabled ? 'users.unified.twoFactorYes' : 'users.unified.twoFactorNo'))}</td>
                            </tr>
                        `,
                    )
                    .join('');

                return `
                    <section class="group">
                        <div class="group-header">
                            <h2>${escapeHtml(group.title)}</h2>
                            <span>${group.rows.length}</span>
                        </div>
                        <table>
                            <thead>
                                <tr>
                                    <th>${escapeHtml(t('users.unified.columns.user'))}</th>
                                    <th>${escapeHtml(t('users.unified.columns.userName'))}</th>
                                    <th>${escapeHtml(t('users.unified.columns.tenant'))}</th>
                                    <th>${escapeHtml(t('users.unified.columns.role'))}</th>
                                    <th>${escapeHtml(t('users.unified.columns.status'))}</th>
                                    <th>${escapeHtml(t('users.unified.columns.lastLogin'))}</th>
                                    <th>${escapeHtml(t('users.unified.columns.twoFactor'))}</th>
                                </tr>
                            </thead>
                            <tbody>${tableRows}</tbody>
                        </table>
                    </section>
                `;
            })
            .join('');

        popup.document.write(`
            <!doctype html>
            <html>
                <head>
                    <meta charset="utf-8" />
                    <title>${escapeHtml(t('users.unified.export.button'))}</title>
                    <style>
                        body { font-family: Arial, sans-serif; margin: 24px; color: #111; }
                        h1 { margin-bottom: 8px; }
                        p { color: #666; margin-bottom: 24px; }
                        .group { margin-bottom: 24px; }
                        .group-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
                        .group-header h2 { margin: 0; font-size: 18px; }
                        .group-header span { background: #f0f0f0; border-radius: 999px; padding: 2px 10px; font-size: 12px; }
                        table { width: 100%; border-collapse: collapse; margin-top: 8px; }
                        th, td { border: 1px solid #d9d9d9; padding: 8px; text-align: left; font-size: 12px; }
                        th { background: #fafafa; }
                        @media print { body { margin: 12px; } .group { page-break-inside: avoid; } }
                    </style>
                </head>
                <body>
                    <h1>${escapeHtml(t('users.unified.export.button'))}</h1>
                    <p>${escapeHtml(formatDateTime(new Date().toISOString(), '', { hour: '2-digit', minute: '2-digit', second: '2-digit' }))}</p>
                    ${sections}
                </body>
            </html>
        `);
        popup.document.close();
        popup.focus();
        popup.print();
        message.success(t('users.unified.export.messages.pdfReady'));
    };

    const columns: ColumnsType<UnifiedAdminUserRow> = useMemo(
        () => [
            {
                title: t('users.unified.columns.userName'),
                key: 'userName',
                width: 150,
                ellipsis: true,
                render: (_: unknown, row) => row.user.userName?.trim() || '—',
                sorter: (a, b) =>
                    (a.user.userName ?? '').localeCompare(b.user.userName ?? '', undefined, {
                        sensitivity: 'base',
                    }),
            },
            {
                title: t('users.unified.columns.user'),
                key: 'user',
                width: 250,
                render: (_: unknown, row) => (
                    <Flex gap="small" align="center">
                        <Avatar style={{ backgroundColor: getColorFromEmail(row.email || row.name) }}>
                            {avatarCharacter(row)}
                        </Avatar>
                        <Flex vertical gap={0} style={{ minWidth: 0, flex: 1 }}>
                            <Typography.Text strong ellipsis>
                                {avatarLabel(row)}
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
                render: (v: string | null | undefined) => renderLastLoginCell(v),
            },
            ...(isSuperAdminActor
                ? [
                      {
                          title: t('users.create.password'),
                          key: 'password',
                          width: '16%',
                          render: (_: unknown, row: UnifiedAdminUserRow) => renderPasswordCell(row),
                      },
                  ]
                : []),
            {
                title: t('users.unified.columns.twoFactor'),
                key: 'twoFactor',
                width: '8%',
                align: 'center',
                render: (_: unknown, row) => renderTwoFactorBadge(row.twoFactorEnabled),
            },
            {
                title: t('users.unified.columns.actions'),
                key: 'actions',
                width: 150,
                fixed: 'right',
                render: (_: unknown, row) => renderActions(row),
            },
        ],
        [
            t,
            formatLocale,
            policy,
            isSuperAdminActor,
            onView,
            onEdit,
            onDeactivate,
            onReactivate,
            onResetPassword,
            onManagePermissions,
            removeMutation.isPending,
            currentUserId,
        ],
    );

    const isTableLoading = isLoading || (isFetching && !!searchParam);
    const exportMenuItems: MenuProps['items'] = [
        {
            key: 'csv',
            label: t('users.unified.export.csv'),
            icon: <FileExcelOutlined />,
            disabled: filteredRows.length === 0,
            onClick: handleExportCsv,
        },
        {
            key: 'pdf',
            label: t('users.unified.export.pdf'),
            icon: <FilePdfOutlined />,
            disabled: filteredRows.length === 0,
            onClick: handleExportPdf,
        },
    ];
    const groupItems = groupedRows.map((group) => ({
        key: group.key,
        label: (
            <div className={styles.groupHeader}>
                <Space size="small" className={styles.groupHeaderMain}>
                    {group.kind === 'platform' ? <GlobalOutlined /> : <ShopOutlined />}
                    <Typography.Text strong ellipsis className={styles.groupHeaderTitle}>
                        {group.title}
                    </Typography.Text>
                </Space>
                <Badge count={group.rows.length} />
            </div>
        ),
        children: (
            <div className={styles.tableWrap}>
                <Table<UnifiedAdminUserRow>
                    rowKey="key"
                    dataSource={group.rows}
                    columns={columns}
                    scroll={{ x: TABLE_SCROLL_X }}
                    pagination={false}
                />
            </div>
        ),
    }));

    return (
        <Space
            orientation="vertical"
            size="middle"
            style={{ width: '100%' }}
            data-testid="unified-admin-users-view"
        >
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {isTenantScoped ? t('users.list.pageIntro') : t('users.unified.pageIntro')}
            </Typography.Paragraph>

            <Card size="small" styles={{ body: { padding: 16 } }}>
                <div className={styles.filtersBar}>
                    <Space wrap size={[12, 12]}>
                        <Input.Search
                            allowClear
                            placeholder={t('users.unified.filters.searchPlaceholder')}
                            style={{ width: 250 }}
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            onSearch={handleSearch}
                        />
                        {!isTenantScoped ? (
                            <TenantFilter
                                value={selectedTenant}
                                onChange={handleTenantFilterChange}
                                includePlatformOption={isSuperAdminActor}
                                style={{ width: 180 }}
                            />
                        ) : null}
                        <Select
                            allowClear
                            placeholder={t('users.unified.filters.roleFilter')}
                            style={{ width: 150 }}
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
                            style={{ width: 130 }}
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
                        <Button icon={<ReloadOutlined />} onClick={handleResetFilters} disabled={!hasCustomFilters}>
                            {t('users.list.clearAllFilters')}
                        </Button>
                    </Space>
                </div>
            </Card>

            {isError ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('users.list.errorLoad')}
                    action={
                        <Button size="small" onClick={refetchAll}>
                            {t('users.list.retry')}
                        </Button>
                    }
                />
            ) : null}

            <Flex wrap="wrap" gap="small">
                <Dropdown menu={{ items: exportMenuItems }} trigger={['click']}>
                    <Button icon={<DownloadOutlined />} disabled={filteredRows.length === 0}>
                        {t('users.unified.export.button')}
                    </Button>
                </Dropdown>
                {canCreateTenantUsers ? (
                    <>
                        <Button type="primary" icon={<UserAddOutlined />} onClick={() => setCreateOpen(true)}>
                            {t('users.create.action')}
                        </Button>
                        {isSuperAdminActor && policy.canCreate ? (
                            <Button icon={<ImportOutlined />} onClick={() => setBulkImportOpen(true)}>
                                Massenimport
                            </Button>
                        ) : null}
                    </>
                ) : null}
                {isSuperAdminActor && policy.canCreate ? (
                    <Button icon={<UserOutlined />} onClick={onCreatePlatformUser}>
                        {t('users.page.createPlatformAdmin')}
                    </Button>
                ) : null}
            </Flex>

            <div className={styles.tableWrap}>
                {isTableLoading || groupedRows.length === 0 ? (
                    <Table<UnifiedAdminUserRow>
                        rowKey="key"
                        sticky
                        loading={isTableLoading}
                        dataSource={groupedRows.length === 0 ? filteredRows : []}
                        columns={columns}
                        scroll={{ x: TABLE_SCROLL_X, y: TABLE_SCROLL_Y }}
                        pagination={false}
                        locale={{
                            emptyText: (
                                <Empty
                                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                                    description={t('users.unified.empty')}
                                />
                            ),
                        }}
                    />
                ) : (
                    <Collapse
                        accordion
                        className={styles.groupCollapse}
                        items={groupItems}
                        defaultActiveKey={groupItems[0] ? [groupItems[0].key] : undefined}
                    />
                )}
            </div>

            {canCreateTenantUsers ? (
                <>
                    <CreateUserModal
                        open={createOpen}
                        variant="usersPage"
                        isSuperAdmin={isSuperAdminActor}
                        tenantId={createFixedTenantId}
                        tenantRows={createTenants}
                        tenantsLoading={createTenantsLoading}
                        confirmLoading={createMutation.isPending || quickPlatformMutation.isPending}
                        onClose={() => setCreateOpen(false)}
                        onComplete={() => setCreateOpen(false)}
                        allowDeferredTenantAssignment={isSuperAdminActor}
                        onAssignTenants={
                            isSuperAdminActor
                                ? (userId, tenantIds) =>
                                      assignTenantsMutation.mutateAsync({ userId, tenantIds })
                                : undefined
                        }
                        onSubmit={(values) =>
                            createMutation.mutateAsync({
                                email: values.email,
                                firstName: values.firstName,
                                lastName: values.lastName,
                                role: values.role,
                                tenantId: values.tenantId,
                                isOwner: values.isOwner,
                            })
                        }
                        quickMode={
                            tenantFilter !== FILTER_PLATFORM
                                ? {
                                      onSubmit: (values) => quickMutation.mutateAsync(values),
                                      ...(isSuperAdminActor
                                          ? {
                                                onSubmitWithoutTenant: (values) =>
                                                    quickPlatformMutation.mutateAsync(values),
                                            }
                                          : {}),
                                  }
                                : undefined
                        }
                    />
                    <BulkImportModal
                        open={bulkImportOpen}
                        onClose={() => setBulkImportOpen(false)}
                        onSuccess={() => {
                            void refetchAll();
                        }}
                    />
                    <ResetPasswordModal
                        open={!!resetRow}
                        tenantId={resetRow?.tenantId ?? ''}
                        user={
                            resetRow
                                ? {
                                      userId: resetRow.userId,
                                      userName: resetRow.userName ?? resetRow.email ?? '',
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

            {isSuperAdminActor ? (
                <PasswordViewModal
                    open={!!passwordRow}
                    userId={passwordRow?.userId ?? ''}
                    userEmail={passwordRow?.email ?? ''}
                    onClose={() => setPasswordRow(null)}
                />
            ) : null}

            {usernameEditUser?.id ? (
                <EditUsernameModal
                    open={!!usernameEditUser}
                    userId={usernameEditUser.id}
                    currentUsername={usernameEditUser.userName ?? ''}
                    userEmail={usernameEditUser.email}
                    onClose={() => setUsernameEditUser(null)}
                    onSuccess={() => {
                        void refetchAll();
                        void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
                        setUsernameEditUser(null);
                    }}
                />
            ) : null}
        </Space>
    );
}
