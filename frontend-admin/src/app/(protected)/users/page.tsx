'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * User management – RKSV/DSGVO-aligned user lifecycle.
 * Table: name, email, role, branch, status, last login, actions.
 * Filters: role, status, branch, search. Drawer create/edit, deactivate (reason), reactivate, activity timeline tab.
 */
import React, { useState, useMemo, useEffect, useCallback, useRef } from 'react';
import { Modal, Card, Typography, Tag, Space, Button, Select, Alert, Empty, Flex, Tooltip, Descriptions } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import {
    UserOutlined,
    SearchOutlined,
    ReloadOutlined,
    ClearOutlined,
} from '@ant-design/icons';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient, useMutation, useQuery } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { useRoles } from '@/features/users/hooks/useRoles';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { useI18n } from '@/i18n/I18nProvider';
import { formatNumber } from '@/i18n/formatting';
import {
    listQueryKey,
    rolesQueryKey,
    rolesWithPermissionsQueryKey,
    permissionsCatalogQueryKey,
    createUser as gatewayCreateUser,
    updateUser as gatewayUpdateUser,
    getUserById,
    getUserByIdQueryKey,
    deactivateUser as gatewayDeactivateUser,
    reactivateUser as gatewayReactivateUser,
    resetPassword as gatewayResetPassword,
    createRole as gatewayCreateRole,
    updateRolePermissions as gatewayUpdateRolePermissions,
    deleteRole as gatewayDeleteRole,
    normalizeError,
    type UserInfo,
    type CreateUserRequest,
    type UpdateUserRequest,
} from '@/features/users/api/usersGateway';
import { UserDetailDrawer } from '@/features/users/components/UserDetailDrawer';
import { EditUsernameModal } from '@/features/users/components/EditUsernameModal';
import { UsersTable } from '@/features/users/components/UsersTable';
import { UserFormDrawer, type UserFormSubmitValues } from '@/features/users/components/UserFormDrawer';
import { RoleManagementDrawer } from '@/features/users/components/RoleManagementDrawer';
import { UserPermissionsModal } from '@/features/users/components/UserPermissionsModal';
import {
    CreateRoleModal,
    DeactivateUserModal,
    ResetPasswordUserModal,
} from '@/features/users/components/UsersPageActionModals';
import { usersCopy, mapBackendPasswordErrorToGerman } from '@/features/users/constants/copy';
import { createUsersFormRules } from '@/features/users/constants/validation';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { getTenantSwitcherLicenseBadge } from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { UnifiedAdminUsersView } from '@/features/users/components/UnifiedAdminUsersView';
import { DevOrphanedUsersCleanupButton } from '@/features/users/components/DevOrphanedUsersCleanupButton';
import { createPlatformUser, updateUserTenants } from '@/features/users/api/users';
import { isPlatformUserRole } from '@/features/users/utils/userScope';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';

function fullName(record: UserInfo): string {
    const first = record.firstName ?? '';
    const last = record.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || record.userName || record.id || '—';
}

const CANONICAL_ROLE_VALUES = [
    'SuperAdmin',
    'Manager',
    'Cashier',
    'Waiter',
    'Kitchen',
    'ReportViewer',
    'Accountant',
] as const;

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;

const modalFormRulesContext = {
  requiredMessage: usersCopy.validationRequired,
  emailInvalidMessage: usersCopy.validationEmail,
  passwordMinMessage: usersCopy.validationPasswordMin,
  passwordPolicyMessage: usersCopy.validationPasswordPolicy,
  maxLengthMessage: usersCopy.validationMaxLength,
  reasonRequiredMessage: usersCopy.reasonRequiredMessage,
  roleNameRequiredMessage: usersCopy.roleNameRequired,
};

function isCanonicalRoleName(roleName: string): roleName is (typeof CANONICAL_ROLE_VALUES)[number] {
    return (CANONICAL_ROLE_VALUES as readonly string[]).includes(roleName);
}

/** Aktiver Mandant-Kontext (Badge + API) — einheitlich über useCurrentTenant. */
function UsersPageActiveTenantContext() {
    const { t } = useI18n();
    const {
        hasAuthToken,
        isSuperAdminPlatformMode,
        isRealTenantSlug,
        tenantName,
        tenantSlug,
        tenantId,
        isTenantSuspended,
        licenseValidUntilUtc,
        licenseKey,
        isTenantRecordLoading,
        isDevTenantOverride,
        isImpersonating,
    } = useCurrentTenant();

    const licenseBadge = useMemo(
        () =>
            getTenantSwitcherLicenseBadge(
                { licenseValidUntilUtc: licenseValidUntilUtc ?? null, licenseKey: licenseKey ?? null },
                t,
            ),
        [licenseValidUntilUtc, licenseKey, t],
    );

    if (!hasAuthToken || isSuperAdminPlatformMode || !isRealTenantSlug) {
        return null;
    }

    const displayName = tenantName?.trim() || tenantSlug || '—';

    return (
        <Card
            size="small"
            loading={isTenantRecordLoading}
            style={{ marginBottom: 16 }}
            title={t('common.tenant.badgeDualLabel', { name: displayName })}
        >
            {isTenantSuspended ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 12 }}
                    title={t('adminShell.tenant.devSwitcher.suspendedSuffix')}
                />
            ) : null}
            <Flex gap={4} wrap="wrap" style={{ marginBottom: 8 }}>
                {isImpersonating ? (
                    <Tag color="purple">{t('adminShell.tenant.infoCardImpersonationTag')}</Tag>
                ) : null}
                {isDevTenantOverride ? (
                    <Tag color="gold">{t('adminShell.tenant.infoCardDevOverrideTag')}</Tag>
                ) : null}
                {licenseBadge ? (
                    <Tooltip title={licenseBadge.tooltip}>
                        <Tag color={licenseBadge.color}>{licenseBadge.label}</Tag>
                    </Tooltip>
                ) : null}
            </Flex>
            <Descriptions column={1} size="small">
                <Descriptions.Item label={t('adminShell.tenant.infoCardName')}>
                    {tenantName || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.slug')}>
                    <Typography.Text code copyable>
                        {tenantSlug}
                    </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.infoCardId')}>
                    {tenantId ? (
                        <Typography.Text code copyable={{ text: tenantId }}>
                            {tenantId}
                        </Typography.Text>
                    ) : (
                        '—'
                    )}
                </Descriptions.Item>
            </Descriptions>
        </Card>
    );
}

export default function UsersPage() {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const pathname = usePathname();
    const router = useRouter();
    const searchParams = useSearchParams();

    const roleDisplayLabel = useCallback(
        (roleName: string) =>
            isCanonicalRoleName(roleName) ? t(`users.roles.displayNames.${roleName}`) : roleName,
        [t],
    );
    const [roleFilter, setRoleFilter] = useState<string | undefined>();
    const [statusFilter, setStatusFilter] = useState<boolean | undefined>(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [searchInput, setSearchInput] = useState('');
    const [page, setPage] = useState(DEFAULT_PAGE);
    const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

    const [createOpen, setCreateOpen] = useState(false);
    /** Edit: only store selected user id. Detail is fetched by useQuery below; never use list row data for edit form. */
    const [editUserId, setEditUserId] = useState<string | null>(null);
    const [detailUser, setDetailUser] = useState<UserInfo | null>(null);
    const [deactivateUserRecord, setDeactivateUserRecord] = useState<UserInfo | null>(null);
    const [reactivateUserRecord, setReactivateUserRecord] = useState<UserInfo | null>(null);
    const [resetPasswordUser, setResetPasswordUser] = useState<UserInfo | null>(null);
    const [usernameEditUser, setUsernameEditUser] = useState<UserInfo | null>(null);
    const [permissionsUser, setPermissionsUser] = useState<UserInfo | null>(null);
    /** Backend validation error shown inside reset password modal (German); cleared when modal closes. */
    const [resetPasswordValidationError, setResetPasswordValidationError] = useState<string | null>(null);
    const [createRoleOpen, setCreateRoleOpen] = useState(false);
    const [roleManagementDrawerOpen, setRoleManagementDrawerOpen] = useState(false);
    const { user: currentUser } = useAuth();
    const policy = useUsersPolicy();
    const isSuperAdminLayout = isSuperAdmin(currentUser?.role);

    useEffect(() => {
        if (isSuperAdminLayout && pathname === '/users') {
            const params = new URLSearchParams(
                typeof window !== 'undefined' ? window.location.search : '',
            );
            params.delete('create');
            params.delete('platform');
            const search = params.toString();
            router.replace(`/admin/users${search ? `?${search}` : ''}`);
        }
    }, [isSuperAdminLayout, pathname, router]);

    /** Strip command-palette query params; user detail deep link only — never auto-open create modal. */
    const paletteDeepLinkHandledRef = useRef(false);
    useEffect(() => {
        if (!policy.canView) return;
        const create = searchParams.get('create');
        const userId = searchParams.get('userId')?.trim();
        const hasPaletteParams = create === '1' || Boolean(userId);
        if (!hasPaletteParams) {
            paletteDeepLinkHandledRef.current = false;
            return;
        }
        if (paletteDeepLinkHandledRef.current) return;
        paletteDeepLinkHandledRef.current = true;

        const basePath = pathname?.startsWith('/admin/users') ? '/admin/users' : '/users';
        router.replace(basePath, { scroll: false });

        if (userId) {
            void getUserById(userId)
                .then((u) => setDetailUser(u))
                .catch(() => message.error(usersCopy.errorLoadUser));
        }
    }, [searchParams, policy.canView, pathname, router, message]);

    const queryClient = useQueryClient();
    const listParams = useMemo(
        () => ({
            role: roleFilter,
            isActive: statusFilter,
            query: searchTerm.trim() || undefined,
            page,
            pageSize,
        }),
        [roleFilter, statusFilter, searchTerm, page, pageSize]
    );
    const tenantScopedListParams = useMemo(
        () => ({
            ...listParams,
            role: roleFilter,
        }),
        [listParams, roleFilter],
    );

    const { data: listData, isLoading, isFetching, isError, error: listError, refetch } = useUsersList(
        tenantScopedListParams,
        {
            enabled: policy.canView && !isSuperAdminLayout,
        },
    );
    const users = useMemo(() => {
        const items = listData?.items ?? [];
        return items.filter((u) => !isPlatformUserRole(u.role));
    }, [listData?.items]);

    const pagination = listData?.pagination;
    useEffect(() => {
        setPage(DEFAULT_PAGE);
    }, [roleFilter, statusFilter, searchTerm]);

    const usersScopeSummary = useMemo(() => {
        const parts: string[] = [
            t('users.list.scopeLinePage', { page: String(pagination?.page ?? page) }),
            t('users.list.scopeLinePerPage', { pageSize: String(pagination?.pageSize ?? pageSize) }),
            pagination?.totalCount != null
                ? t('users.list.scopeTotalApi', {
                      count: formatNumber(pagination.totalCount, formatLocale, { maximumFractionDigits: 0 }),
                  })
                : t('users.list.scopeTotalLoading'),
        ];
        if (searchTerm.trim()) {
            parts.push(
                t('users.list.scopeSummarySearchPart', {
                    prefix: t('users.list.scopeSearchPrefix'),
                    term: searchTerm.trim(),
                }),
            );
        }
        if (roleFilter) {
            parts.push(
                t('users.list.scopeSummaryRolePart', {
                    prefix: t('users.list.scopeRolePrefix'),
                    role: roleDisplayLabel(roleFilter),
                }),
            );
        }
        if (statusFilter === undefined) {
            parts.push(t('users.list.scopeStatusAll'));
        } else {
            parts.push(
                t('users.list.scopeStatusLine', {
                    prefix: t('users.list.scopeStatusPrefix'),
                    status: statusFilter ? t('users.list.statusActive') : t('users.list.statusInactive'),
                }),
            );
        }
        return parts.join(' · ');
    }, [pagination, page, pageSize, searchTerm, roleFilter, statusFilter, formatLocale, t, roleDisplayLabel]);

    const resetAllFilters = useCallback(() => {
        setSearchInput('');
        setSearchTerm('');
        setRoleFilter(undefined);
        setStatusFilter(true);
        setPage(DEFAULT_PAGE);
        setPageSize(DEFAULT_PAGE_SIZE);
    }, []);

    /** Deviations from default list query: active-only, no role, no search text. */
    const hasNonDefaultListFilters = Boolean(
        searchTerm.trim() || roleFilter || statusFilter !== true,
    );

    const { data: roles, isLoading: rolesLoading } = useRoles({ enabled: policy.canView || !!editUserId });
    const canManageRoles = policy.canCreateRole || policy.canDeleteRole || policy.canEditRolePermissions;
    const { data: rolesWithPermissions, isLoading: rolesWithPermsLoading, isError: rolesWithPermsError, refetch: refetchRolesWithPerms } = useRolesWithPermissions({ enabled: roleManagementDrawerOpen });
    const { data: permissionsCatalog, isLoading: catalogLoading, isError: catalogError, refetch: refetchCatalog } = usePermissionsCatalog({ enabled: roleManagementDrawerOpen });
    // Edit flow: when editUserId is set, fetch full user detail (GET /api/UserManagement/{id}); form is filled from this response only.
    const { data: editUserFull, isLoading: editUserLoading, isError: editUserError, error: editUserFetchError, refetch: refetchEditUser } = useQuery({
        queryKey: getUserByIdQueryKey(editUserId ?? ''),
        queryFn: () => getUserById(editUserId!),
        enabled: !!editUserId,
    });
    const modalRules = useMemo(() => createUsersFormRules(modalFormRulesContext), []);

    // Role list for form + filter: always from full catalog (useRoles). Never from selected user or assigned subset.
    const roleOptions = useMemo(
        () =>
            roles?.map((r) => ({ value: r, label: roleDisplayLabel(r) })) ??
            CANONICAL_ROLE_VALUES.map((value) => ({
                value,
                label: t(`users.roles.displayNames.${value}`),
            })),
        [roles, t, roleDisplayLabel],
    );

    const statusFilterOptions = useMemo(
        () => [
            { value: 'active', label: t('users.list.statusActive') },
            { value: 'inactive', label: t('users.list.statusInactive') },
        ],
        [t],
    );

    const [createPlatformMode, setCreatePlatformMode] = useState(false);

    useEffect(() => {
        setCreateOpen(false);
        setCreatePlatformMode(false);
        return () => {
            setCreateOpen(false);
            setCreatePlatformMode(false);
        };
    }, [pathname]);

    const invalidateAllUserLists = () => {
        void queryClient.invalidateQueries({ queryKey: listQueryKey });
        void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    };

    const createMutation = useMutation({
        mutationFn: (payload: { platform: boolean; data: CreateUserRequest }) =>
            payload.platform
                ? createPlatformUser({
                      email: payload.data.email ?? '',
                      firstName: payload.data.firstName,
                      lastName: payload.data.lastName,
                      role: 'SuperAdmin',
                  }).then(() => undefined)
                : gatewayCreateUser(payload.data),
        onSuccess: (_data, { platform }) => {
            message.success(usersCopy.successCreate);
            if (platform) {
                invalidateAllUserLists();
            } else {
                invalidateAllUserLists();
            }
            setCreateOpen(false);
            setCreatePlatformMode(false);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const updateMutation = useMutation({
        mutationFn: ({ id, data }: { id: string; data: UpdateUserRequest }) => gatewayUpdateUser(id, data),
    });
    const updateUserTenantsMutation = useMutation({
        mutationFn: ({ id, tenantIds }: { id: string; tenantIds: string[] }) => updateUserTenants(id, tenantIds),
    });
    const resetPasswordMutation = useMutation({
        mutationFn: ({ id, data }: { id: string; data: { newPassword: string } }) => gatewayResetPassword(id, data),
        onSuccess: (_data, { id }) => {
            message.success(usersCopy.successResetPassword);
            invalidateAllUserLists();
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setResetPasswordUser(null);
            setResetPasswordValidationError(null);
        },
        onError: (e: unknown) => {
            const err = e as { response?: { status?: number; data?: { errors?: Record<string, string[]> } } };
            const status = err.response?.status;
            const is401 = status === 401;
            const is403 = status === 403;
            const is404 = status === 404;
            const is400 = status === 400;
            const fallback =
                is401 ? usersCopy.sessionExpiredOrUnauthorized
                : is403 ? usersCopy.errorResetPasswordForbidden
                : is404 ? usersCopy.errorResetPasswordUserNotFound
                : usersCopy.errorResetPassword;
            const normalized = normalizeError(e, fallback);
            const data = err.response?.data as { errors?: Record<string, string[]> } | undefined;
            const errors = data?.errors;
            const fieldErrors = errors?.newPassword ?? errors?.NewPassword;
            const firstBackendMessage = Array.isArray(fieldErrors) && fieldErrors.length > 0 ? fieldErrors[0] : normalized.message;
            const displayMessage = mapBackendPasswordErrorToGerman(firstBackendMessage, usersCopy);

            if (is400) {
                setResetPasswordValidationError(displayMessage);
            } else {
                message.error(normalized.message);
            }
        },
    });
    const deactivateMutation = useMutation({
        mutationFn: ({ id, reason }: { id: string; reason: string }) => gatewayDeactivateUser(id, { reason }),
        onSuccess: (_data, { id }) => {
            message.success(usersCopy.successDeactivate);
            invalidateAllUserLists();
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setDeactivateUserRecord(null);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const reactivateMutation = useMutation({
        mutationFn: (id: string) => gatewayReactivateUser(id),
        onSuccess: (_data, id) => {
            message.success(usersCopy.successReactivate);
            invalidateAllUserLists();
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setReactivateUserRecord(null);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const createRoleMutation = useMutation({
        mutationFn: (data: { name: string }) => gatewayCreateRole({ name: data.name.trim() }),
        onSuccess: () => {
            message.success(t('users.messages.roleCreated'));
            queryClient.invalidateQueries({ queryKey: rolesQueryKey });
            queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
            setCreateRoleOpen(false);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const updateRolePermissionsMutation = useMutation({
        mutationFn: ({ roleName, permissions }: { roleName: string; permissions: string[] }) =>
            gatewayUpdateRolePermissions(roleName, permissions),
        onSuccess: () => {
            message.success(usersCopy.successPermissionsSaved);
            queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
            queryClient.invalidateQueries({ queryKey: rolesQueryKey });
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorSavePermissions).message);
        },
    });
    const deleteRoleMutation = useMutation({
        mutationFn: (roleName: string) => gatewayDeleteRole(roleName),
        onSuccess: () => {
            message.success(usersCopy.successRoleDeleted);
            queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
            queryClient.invalidateQueries({ queryKey: rolesQueryKey });
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorDeleteRole).message);
        },
    });
    const handleCreate = (values: CreateUserRequest | UpdateUserRequest) => {
        if (!policy.canCreate) {
            message.error(usersCopy.noPermission);
            return;
        }
        const raw = values as CreateUserRequest;
        const createPayload: CreateUserRequest = {
            ...raw,
            employeeNumber: (raw.employeeNumber ?? '').trim(),
            ...(createPlatformMode ? { role: 'SuperAdmin' } : {}),
        };
        createMutation.mutate({ platform: createPlatformMode, data: createPayload });
    };
    const handleEdit = async (values: UserFormSubmitValues) => {
        if (!editUserId) return;
        if (!policy.canEdit) {
            message.error(usersCopy.noPermission);
            return;
        }
        const { tenantIds, ...restValues } = values as UpdateUserRequest & { tenantIds?: string[] };
        const updatePayload: UpdateUserRequest = {
            ...restValues,
            employeeNumber: (restValues.employeeNumber ?? '').trim(),
        };
        try {
            await updateMutation.mutateAsync({ id: editUserId, data: updatePayload });
            if (isSuperAdminLayout && Array.isArray(tenantIds)) {
                await updateUserTenantsMutation.mutateAsync({ id: editUserId, tenantIds });
                await queryClient.invalidateQueries({ queryKey: ['admin', 'users', editUserId, 'tenants'] });
                message.success(t('users.tenants.manageSaved'));
            } else {
                message.success(usersCopy.successUpdate);
            }
            await queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(editUserId) });
            invalidateAllUserLists();
            await queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${editUserId}`] });
            setEditUserId(null);
        } catch (e: unknown) {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        }
    };
    const handleDeactivateConfirm = (reason: string) => {
        if (!deactivateUserRecord?.id) return;
        if (!policy.canDeactivate) {
            message.error(usersCopy.noPermission);
            return;
        }
        deactivateMutation.mutate({ id: deactivateUserRecord.id, reason });
    };
    const handleReactivate = () => {
        if (!reactivateUserRecord?.id) return;
        if (!policy.canReactivate) {
            message.error(usersCopy.noPermission);
            return;
        }
        reactivateMutation.mutate(reactivateUserRecord.id);
    };
    const handleResetPasswordConfirm = (newPassword: string) => {
        if (!resetPasswordUser?.id) return;
        if (!policy.canResetPassword(resetPasswordUser.role)) {
            message.error(usersCopy.noPermission);
            return;
        }
        resetPasswordMutation.mutate({ id: resetPasswordUser.id, data: { newPassword } });
    };
    const handleCreateRoleConfirm = (name: string) => {
        if (!policy.canCreateRole) {
            message.error(usersCopy.noPermission);
            return;
        }
        createRoleMutation.mutate({ name });
    };
    const handleClearResetPasswordValidationError = useCallback(() => {
        setResetPasswordValidationError(null);
    }, []);

    const handleRoleManagementRetry = () => {
        refetchRolesWithPerms();
        refetchCatalog();
    };

    const handleSaveRolePermissions = async (roleName: string, permissions: string[]) => {
        await updateRolePermissionsMutation.mutateAsync({ roleName, permissions });
    };

    const handleDeleteRole = async (roleName: string) => {
        await deleteRoleMutation.mutateAsync(roleName);
    };

    const handleUsernameUpdated = useCallback(
        (userId: string, result: { newUsername: string }) => {
            setDetailUser((prev) =>
                prev?.id === userId ? { ...prev, userName: result.newUsername } : prev,
            );
            invalidateAllUserLists();
            void refetch();
            if (editUserId === userId) {
                void refetchEditUser();
            }
        },
        [editUserId, refetch, refetchEditUser, queryClient],
    );

    if (!policy.canView) {
        return (
            <AdminPageShell>
                <AdminPageHeader
                    title={t('users.page.title')}
                    breadcrumbs={[adminOverviewCrumb(t), { title: t('users.page.title') }]}
                />
                <Alert
                    type="warning"
                    showIcon
                    title={t('users.page.accessDeniedTitle')}
                    description={t('users.page.accessDeniedDescription')}
                />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('users.page.title')}
                breadcrumbs={[adminOverviewCrumb(t), { title: t('users.page.title') }]}
                actions={
                    <Space wrap>
                        {!isSuperAdminLayout ? (
                            <Tooltip title={t('common.toolbar.refetchHint')}>
                                <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                                    {t('users.list.actionRefresh')}
                                </Button>
                            </Tooltip>
                        ) : null}
                        {policy.canCreate && !isSuperAdminLayout && (
                            <Button
                                type="primary"
                                icon={<UserOutlined />}
                                onClick={() => {
                                    setCreatePlatformMode(false);
                                    setCreateOpen(true);
                                }}
                            >
                                {t('users.page.createUser')}
                            </Button>
                        )}
                        {policy.canCreateRole && (
                            <Button icon={<UserOutlined />} onClick={() => setCreateRoleOpen(true)}>
                                {t('users.page.createRole')}
                            </Button>
                        )}
                        {canManageRoles && (
                            <Button type="default" onClick={() => setRoleManagementDrawerOpen(true)}>
                                {t('users.page.manageRoles')}
                            </Button>
                        )}
                        {isSuperAdminLayout ? (
                            <DevOrphanedUsersCleanupButton invalidatePlatformUsers />
                        ) : (
                            <DevOrphanedUsersCleanupButton onTenantListRefetch={() => void refetch()} />
                        )}
                    </Space>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                    {isSuperAdminLayout ? t('users.unified.pageIntro') : t('users.list.pageIntro')}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                    {t('users.list.forensicsHintLead')}{' '}
                    <Link href="/audit-logs">{t('users.list.forensicsLinkAuditLog')}</Link>
                    {' · '}
                    <Link href="/rksv/verifications">{t('users.list.forensicsLinkVerifications')}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            <UsersPageActiveTenantContext />

            {isSuperAdminLayout ? (
                <UnifiedAdminUsersView
                    policy={policy}
                    roleDisplayLabel={roleDisplayLabel}
                    currentUserId={currentUser?.id}
                    onView={setDetailUser}
                    onEdit={(id) => setEditUserId(id)}
                    onDeactivate={setDeactivateUserRecord}
                    onReactivate={setReactivateUserRecord}
                    onResetPassword={setResetPasswordUser}
                    onManagePermissions={policy.canManagePermissions ? setPermissionsUser : undefined}
                    onCreatePlatformUser={() => {
                        setCreatePlatformMode(true);
                        setCreateOpen(true);
                    }}
                />
            ) : (
                <>
                    <Card size="small" title={t('users.list.filterCardTitle')}>
                        <Flex wrap="wrap" gap="small" align="center">
                            <Typography.Text type="secondary">{t('users.list.filterBandLabel')}</Typography.Text>
                            <Input.Search
                                placeholder={t('users.list.searchPlaceholder')}
                                allowClear
                                value={searchInput}
                                onChange={(e) => {
                                    const v = e.target.value;
                                    setSearchInput(v);
                                    if (!v) setSearchTerm('');
                                }}
                                onSearch={(v) => setSearchTerm(v ?? '')}
                                style={{ width: 260 }}
                                enterButton={<SearchOutlined />}
                            />
                            <Select
                                placeholder={t('users.list.filterRolePlaceholder')}
                                allowClear
                                style={{ width: 140 }}
                                value={roleFilter}
                                onChange={setRoleFilter}
                                options={roleOptions}
                            />
                            <Select
                                placeholder={t('users.list.filterStatusPlaceholder')}
                                allowClear
                                style={{ width: 120 }}
                                value={statusFilter === undefined ? undefined : statusFilter ? 'active' : 'inactive'}
                                onChange={(v) => setStatusFilter(v === undefined ? undefined : v === 'active')}
                                options={statusFilterOptions}
                            />
                            <Button icon={<ClearOutlined />} onClick={resetAllFilters}>
                                {t('users.list.clearAllFilters')}
                            </Button>
                        </Flex>
                    </Card>

                    {hasNonDefaultListFilters ? (
                        <div style={{ marginTop: 4 }}>
                            <Space wrap size={[8, 8]} align="center">
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {t('users.list.activeFiltersLabel')}
                                </Typography.Text>
                                {searchTerm.trim() ? (
                                    <Tag
                                        closable
                                        onClose={() => {
                                            setSearchInput('');
                                            setSearchTerm('');
                                        }}
                                    >
                                        {t('users.list.scopeSearchChip', {
                                            prefix: t('users.list.scopeSearchPrefix'),
                                            term: searchTerm.trim(),
                                        })}
                                    </Tag>
                                ) : null}
                                {roleFilter ? (
                                    <Tag closable onClose={() => setRoleFilter(undefined)}>
                                        {t('users.list.scopeRoleChip', {
                                            prefix: t('users.list.scopeRolePrefix'),
                                            role: roleDisplayLabel(roleFilter),
                                        })}
                                    </Tag>
                                ) : null}
                                <Tag
                                    closable
                                    onClose={() => setStatusFilter(true)}
                                    color={statusFilter === undefined ? 'purple' : statusFilter ? 'green' : 'red'}
                                >
                                    {t('users.list.scopeStatusChip', {
                                        prefix: t('users.list.scopeStatusPrefix'),
                                        status:
                                            statusFilter === undefined
                                                ? t('users.list.statusAll')
                                                : statusFilter
                                                  ? t('users.list.statusActive')
                                                  : t('users.list.statusInactive'),
                                    })}
                                </Tag>
                                <Button type="link" size="small" onClick={resetAllFilters}>
                                    {t('users.list.clearAllFilters')}
                                </Button>
                            </Space>
                        </div>
                    ) : null}

                    <AdminPageScopeSummary label={t('users.list.scopeSummaryLabel')}>
                        {usersScopeSummary}
                        {isFetching && !isLoading && !isError ? (
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {' '}
                                ({t('users.list.listRefreshingHint')})
                            </Typography.Text>
                        ) : null}
                    </AdminPageScopeSummary>

                    {isError && (
                        <Alert
                            type="error"
                            showIcon
                            title={t('users.list.errorLoad')}
                            description={
                                normalizeError(listError, t('users.list.errorLoadDetailFallback')).message ||
                                t('users.list.errorLoadDetailFallback')
                            }
                            action={
                                <Space orientation="vertical" size="small">
                                    <Button size="small" onClick={() => refetch()}>
                                        {t('users.list.retry')}
                                    </Button>
                                    <Button size="small" type="link" onClick={resetAllFilters} style={{ padding: 0, height: 'auto' }}>
                                        {t('users.list.clearAllFilters')}
                                    </Button>
                                </Space>
                            }
                        />
                    )}

                    <UsersTable
                        users={users}
                        loading={isLoading}
                        policy={policy}
                        currentUserId={currentUser?.id}
                        onView={setDetailUser}
                        onEdit={(id) => setEditUserId(id)}
                        onDeactivate={setDeactivateUserRecord}
                        onReactivate={setReactivateUserRecord}
                        onResetPassword={setResetPasswordUser}
                        onManagePermissions={policy.canManagePermissions ? setPermissionsUser : undefined}
                        onUsernameEdit={policy.canEdit ? setUsernameEditUser : undefined}
                        virtual={shouldUseAdminTableVirtual(users.length)}
                        scroll={adminTableScrollXy(1280, users.length)}
                        pagination={{
                            current: pagination?.page ?? DEFAULT_PAGE,
                            pageSize: pagination?.pageSize ?? DEFAULT_PAGE_SIZE,
                            total: pagination?.totalCount ?? 0,
                            showSizeChanger: true,
                            pageSizeOptions: [10, 20, 50],
                            showTotal: (total, range) => {
                                if (total <= 0) return t('users.list.paginationZeroResults');
                                const from = range[0] ?? 0;
                                const to = range[1] ?? 0;
                                return t('users.list.paginationRange', {
                                    from: formatNumber(from, formatLocale, { maximumFractionDigits: 0 }),
                                    to: formatNumber(to, formatLocale, { maximumFractionDigits: 0 }),
                                    total: formatNumber(total, formatLocale, { maximumFractionDigits: 0 }),
                                });
                            },
                            onChange: (newPage, newPageSize) => {
                                setPage(newPage);
                                if (newPageSize != null) setPageSize(newPageSize);
                            },
                        }}
                        emptyDescription={
                            <div>
                                <Typography.Paragraph style={{ marginBottom: 4 }}>
                                    {hasNonDefaultListFilters
                                        ? t('users.list.emptyListWithFilters')
                                        : t('users.list.emptyList')}
                                </Typography.Paragraph>
                                {!hasNonDefaultListFilters ? (
                                    <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0 }}>
                                        {t('users.list.emptyListDefaultHint')}
                                    </Typography.Paragraph>
                                ) : null}
                            </div>
                        }
                    />
                </>
            )}

            {createOpen ? (
                <UserFormDrawer
                    open
                    onClose={() => {
                        setCreateOpen(false);
                        setCreatePlatformMode(false);
                    }}
                    mode="create"
                    createVariant={createPlatformMode ? 'platform' : 'default'}
                    roleOptions={roleOptions}
                    rolesLoading={rolesLoading}
                    onSubmit={handleCreate}
                    loading={createMutation.isPending}
                />
            ) : null}
            {editUserId ? (
                <UserFormDrawer
                    key={editUserId}
                    open
                    onClose={() => setEditUserId(null)}
                    mode="edit"
                    user={editUserFull ?? undefined}
                    initialLoading={editUserLoading}
                    fetchError={editUserError ? (editUserFetchError ?? null) : null}
                    onRetryFetch={refetchEditUser}
                    roleOptions={roleOptions}
                    rolesLoading={rolesLoading}
                    onSubmit={handleEdit}
                    loading={updateMutation.isPending || updateUserTenantsMutation.isPending}
                    canManageTenants={isSuperAdminLayout}
                />
            ) : null}

            <UserDetailDrawer
                open={!!detailUser}
                onClose={() => setDetailUser(null)}
                user={detailUser}
                canEditUsername={policy.canEdit}
                onUsernameUpdated={handleUsernameUpdated}
            />

            {usernameEditUser?.id ? (
                <EditUsernameModal
                    open={!!usernameEditUser}
                    userId={usernameEditUser.id}
                    currentUsername={usernameEditUser.userName ?? ''}
                    userEmail={usernameEditUser.email}
                    onClose={() => setUsernameEditUser(null)}
                    onSuccess={(result) => {
                        handleUsernameUpdated(usernameEditUser.id!, result);
                        setUsernameEditUser(null);
                    }}
                />
            ) : null}

            {deactivateUserRecord ? (
                <DeactivateUserModal
                    user={deactivateUserRecord}
                    onCancel={() => setDeactivateUserRecord(null)}
                    onConfirm={handleDeactivateConfirm}
                    confirmLoading={deactivateMutation.isPending}
                    reasonRules={modalRules.reason}
                />
            ) : null}

            <Modal
                title={usersCopy.reactivateUser}
                open={!!reactivateUserRecord}
                onOk={handleReactivate}
                onCancel={() => setReactivateUserRecord(null)}
                okText={usersCopy.okReactivate}
                confirmLoading={reactivateMutation.isPending}
                destroyOnHidden
            >
                {reactivateUserRecord && (
                    <p>
                        <strong>{fullName(reactivateUserRecord)}</strong> {usersCopy.confirmReactivate}
                    </p>
                )}
            </Modal>

            {resetPasswordUser ? (
                <ResetPasswordUserModal
                    user={resetPasswordUser}
                    onCancel={() => {
                        setResetPasswordUser(null);
                        setResetPasswordValidationError(null);
                    }}
                    onConfirm={handleResetPasswordConfirm}
                    confirmLoading={resetPasswordMutation.isPending}
                    passwordRules={modalRules.newPassword}
                    validationError={resetPasswordValidationError}
                    onClearValidationError={handleClearResetPasswordValidationError}
                />
            ) : null}

            {createRoleOpen ? (
                <CreateRoleModal
                    onCancel={() => setCreateRoleOpen(false)}
                    onConfirm={handleCreateRoleConfirm}
                    confirmLoading={createRoleMutation.isPending}
                    roleNameRules={modalRules.roleName}
                />
            ) : null}

            <RoleManagementDrawer
                open={roleManagementDrawerOpen}
                onClose={() => setRoleManagementDrawerOpen(false)}
                roles={rolesWithPermissions}
                catalog={permissionsCatalog}
                rolesLoading={rolesWithPermsLoading}
                catalogLoading={catalogLoading}
                rolesError={rolesWithPermsError}
                catalogError={catalogError}
                onRetry={handleRoleManagementRetry}
                canCreateRole={policy.canCreateRole}
                canDeleteRole={policy.canDeleteRole}
                canEditRolePermissions={policy.canEditRolePermissions}
                onCreateRole={() => setCreateRoleOpen(true)}
                onSavePermissions={handleSaveRolePermissions}
                onDeleteRole={handleDeleteRole}
                saveLoading={updateRolePermissionsMutation.isPending}
                deleteLoading={deleteRoleMutation.isPending}
            />

            {permissionsUser?.id ? (
                <UserPermissionsModal
                    open
                    userId={permissionsUser.id}
                    userName={fullName(permissionsUser)}
                    userRole={permissionsUser.role}
                    onClose={() => setPermissionsUser(null)}
                />
            ) : null}
        </AdminPageShell>
    );
}
