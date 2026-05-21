'use client';

/**
 * User management – RKSV/DSGVO-aligned user lifecycle.
 * Table: name, email, role, branch, status, last login, actions.
 * Filters: role, status, branch, search. Drawer create/edit, deactivate (reason), reactivate, activity timeline tab.
 */
import React, { useState, useMemo, useEffect, useCallback } from 'react';
import {
    Table,
    Card,
    Typography,
    Tag,
    Space,
    Button,
    Avatar,
    Select,
    Modal,
    Form,
    Input,
    message,
    Alert,
    Empty,
    Flex,
    Tooltip,
    Tabs,
} from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import {
    UserOutlined,
    EditOutlined,
    StopOutlined,
    CheckCircleOutlined,
    EyeOutlined,
    SearchOutlined,
    KeyOutlined,
    ReloadOutlined,
    ClearOutlined,
} from '@ant-design/icons';
import Link from 'next/link';
import { useQueryClient, useMutation, useQuery } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { useRoles } from '@/features/users/hooks/useRoles';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime, formatNumber } from '@/i18n/formatting';
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
import { UserFormDrawer } from '@/features/users/components/UserFormDrawer';
import { RoleManagementDrawer } from '@/features/users/components/RoleManagementDrawer';
import { usersCopy, mapBackendPasswordErrorToGerman } from '@/features/users/constants/copy';
import { createUsersFormRules } from '@/features/users/constants/validation';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { PlatformUsersTab } from '@/features/users/components/PlatformUsersTab';
import { TenantUsersTab } from '@/features/users/components/TenantUsersTab';
import { UserInvitationsPanel } from '@/features/users/components/UserInvitationsPanel';
import { usePlatformUsersList } from '@/features/users/hooks/usePlatformUsersList';
import { adminUsersQueryKeys, createPlatformUser } from '@/features/users/api/users';
import { isPlatformUserRole } from '@/features/users/utils/userScope';

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

export default function UsersPage() {
    const { t, formatLocale } = useI18n();

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
    /** Backend validation error shown inside reset password modal (German); cleared when modal closes. */
    const [resetPasswordValidationError, setResetPasswordValidationError] = useState<string | null>(null);
    const [createRoleOpen, setCreateRoleOpen] = useState(false);
    const [roleManagementDrawerOpen, setRoleManagementDrawerOpen] = useState(false);
    const [superAdminTab, setSuperAdminTab] = useState<'platform' | 'tenant' | 'invitations'>('platform');

    const { user: currentUser } = useAuth();
    const policy = useUsersPolicy();
    const isSuperAdminLayout = isSuperAdmin(currentUser?.role);

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
    const platformListParams = useMemo(
        () => ({
            isActive: statusFilter,
            query: searchTerm.trim() || undefined,
        }),
        [statusFilter, searchTerm],
    );
    const tenantScopedListParams = useMemo(
        () => ({
            ...listParams,
            role: roleFilter,
        }),
        [listParams, roleFilter],
    );

    const {
        items: platformUsers,
        isLoading: platformUsersLoading,
        isFetching: platformUsersFetching,
        isError: platformUsersError,
        error: platformUsersListError,
        refetch: refetchPlatformUsers,
    } = usePlatformUsersList(platformListParams, {
        enabled: policy.canView && isSuperAdminLayout && superAdminTab === 'platform',
    });

    const { data: listData, isLoading, isFetching, isError, error: listError, refetch } = useUsersList(
        tenantScopedListParams,
        {
            enabled: policy.canView && !isSuperAdminLayout,
        },
    );
    const users = useMemo(() => {
        if (isSuperAdminLayout) return platformUsers;
        const items = listData?.items ?? [];
        return items.filter((u) => !isPlatformUserRole(u.role));
    }, [isSuperAdminLayout, platformUsers, listData?.items]);

    const platformListLoading = isSuperAdminLayout ? platformUsersLoading : isLoading;
    const platformListFetching = isSuperAdminLayout ? platformUsersFetching : isFetching;
    const platformListIsError = isSuperAdminLayout ? platformUsersError : isError;
    const platformListError = isSuperAdminLayout ? platformUsersListError : listError;
    const platformListRefetch = isSuperAdminLayout ? refetchPlatformUsers : refetch;
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

    const [deactivateForm] = Form.useForm();
    const [resetPasswordForm] = Form.useForm();
    const [createRoleForm] = Form.useForm<{ name: string }>();

    const [createPlatformMode, setCreatePlatformMode] = useState(false);

    const createMutation = useMutation({
        mutationFn: (payload: { platform: boolean; data: CreateUserRequest }) =>
            payload.platform
                ? createPlatformUser({
                      userName: payload.data.userName!,
                      password: payload.data.password!,
                      email: payload.data.email ?? undefined,
                      firstName: payload.data.firstName!,
                      lastName: payload.data.lastName!,
                      employeeNumber: payload.data.employeeNumber ?? undefined,
                      taxNumber: payload.data.taxNumber ?? undefined,
                      notes: payload.data.notes ?? undefined,
                  }).then(() => undefined)
                : gatewayCreateUser(payload.data),
        onSuccess: (_data, { platform }) => {
            message.success(usersCopy.successCreate);
            if (platform) {
                void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.platform() });
            } else {
                queryClient.invalidateQueries({ queryKey: listQueryKey });
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
        onSuccess: (_data, { id }) => {
            message.success(usersCopy.successUpdate);
            queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(id) });
            queryClient.invalidateQueries({ queryKey: listQueryKey });
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setEditUserId(null);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const resetPasswordMutation = useMutation({
        mutationFn: ({ id, data }: { id: string; data: { newPassword: string } }) => gatewayResetPassword(id, data),
        onSuccess: (_data, { id }) => {
            message.success(usersCopy.successResetPassword);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setResetPasswordUser(null);
            resetPasswordForm.resetFields();
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
                resetPasswordForm.setFields([{ name: 'newPassword', errors: [displayMessage] }]);
            } else {
                message.error(normalized.message);
            }
        },
    });
    const deactivateMutation = useMutation({
        mutationFn: ({ id, reason }: { id: string; reason: string }) => gatewayDeactivateUser(id, { reason }),
        onSuccess: (_data, { id }) => {
            message.success(usersCopy.successDeactivate);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setDeactivateUserRecord(null);
            deactivateForm.resetFields();
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const reactivateMutation = useMutation({
        mutationFn: (id: string) => gatewayReactivateUser(id),
        onSuccess: (_data, id) => {
            message.success(usersCopy.successReactivate);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
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
            createRoleForm.resetFields();
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
    useEffect(() => {
        if (resetPasswordUser) {
            resetPasswordForm.resetFields();
            setResetPasswordValidationError(null);
        }
    }, [resetPasswordUser, resetPasswordForm]);

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
    const handleEdit = (values: UpdateUserRequest) => {
        if (!editUserId) return;
        if (!policy.canEdit) {
            message.error(usersCopy.noPermission);
            return;
        }
        const updatePayload: UpdateUserRequest = {
            ...values,
            employeeNumber: (values.employeeNumber ?? '').trim(),
        };
        updateMutation.mutate({ id: editUserId, data: updatePayload });
    };
    const handleDeactivate = () => {
        if (!deactivateUserRecord?.id) return;
        if (!policy.canDeactivate) {
            message.error(usersCopy.noPermission);
            return;
        }
        deactivateForm.validateFields().then(
            (values: { reason: string }) => {
                deactivateMutation.mutate({ id: deactivateUserRecord.id!, reason: values.reason });
            },
            () => { /* validation errors shown on form */ }
        );
    };
    const handleReactivate = () => {
        if (!reactivateUserRecord?.id) return;
        if (!policy.canReactivate) {
            message.error(usersCopy.noPermission);
            return;
        }
        reactivateMutation.mutate(reactivateUserRecord.id);
    };
    const handleResetPassword = () => {
        if (!resetPasswordUser?.id) return;
        if (!policy.canResetPassword(resetPasswordUser.role)) {
            message.error(usersCopy.noPermission);
            return;
        }
        resetPasswordForm.validateFields()
            .then((values: { newPassword: string }) => {
                resetPasswordMutation.mutate({ id: resetPasswordUser.id!, data: { newPassword: values.newPassword } });
            })
            .catch(() => { /* validasyon hatası; form alanları zaten hata gösterir */ });
    };
    const handleCreateRole = () => {
        if (!policy.canCreateRole) {
            message.error(usersCopy.noPermission);
            return;
        }
        createRoleForm.validateFields()
            .then((values: { name: string }) => {
                createRoleMutation.mutate({ name: values.name.trim() });
            })
            .catch(() => { /* validasyon hatası */ });
    };

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

    const columns = useMemo(
        () => [
            {
                title: t('users.list.columnName'),
                key: 'user',
                render: (_: unknown, record: UserInfo) => (
                    <Space>
                        <Avatar icon={<UserOutlined />} />
                        <div>
                            <div style={{ fontWeight: 'bold' }}>{fullName(record)}</div>
                            <div style={{ fontSize: '12px', color: '#999' }}>{record.email ?? record.userName ?? '—'}</div>
                        </div>
                    </Space>
                ),
            },
            {
                title: t('users.list.columnEmail'),
                dataIndex: 'email',
                key: 'email',
                ellipsis: true,
                render: (v: string | null) => v ?? '—',
            },
            {
                title: t('users.list.columnRole'),
                dataIndex: 'role',
                key: 'role',
                render: (role: string) => <Tag color="gold">{role ?? '—'}</Tag>,
            },
            {
                title: t('users.list.columnBranch'),
                key: 'branch',
                render: () => t('users.list.branchNotAvailable'),
            },
            {
                title: t('users.list.columnStatus'),
                dataIndex: 'isActive',
                key: 'status',
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
                render: (v: string | null) => (v ? formatDateTime(v, formatLocale) : '—'),
            },
            {
                title: t('users.list.columnActions'),
                key: 'actions',
                render: (_: unknown, record: UserInfo) => (
                    <Space wrap>
                        {policy.canEdit && (
                            <Button
                                size="small"
                                icon={<EyeOutlined />}
                                onClick={() => setDetailUser(record)}
                            >
                                {t('users.list.view')}
                            </Button>
                        )}
                        {policy.canEdit && (
                            <Button
                                size="small"
                                icon={<EditOutlined />}
                                onClick={() => setEditUserId(record.id ?? null)}
                            >
                                {t('users.list.edit')}
                            </Button>
                        )}
                        {policy.canDeactivate && record.isActive && !isPlatformUserRole(record.role) && (
                            <Button
                                size="small"
                                danger
                                icon={<StopOutlined />}
                                onClick={() => setDeactivateUserRecord(record)}
                            >
                                {t('users.list.deactivate')}
                            </Button>
                        )}
                        {policy.canReactivate && !record.isActive && (
                            <Button
                                size="small"
                                type="primary"
                                icon={<CheckCircleOutlined />}
                                onClick={() => setReactivateUserRecord(record)}
                            >
                                {t('users.list.reactivate')}
                            </Button>
                        )}
                        {policy.canResetPassword(record.role) && record.id !== currentUser?.id && (
                            <Button
                                size="small"
                                icon={<KeyOutlined />}
                                onClick={() => setResetPasswordUser(record)}
                            >
                                {t('users.list.resetPassword')}
                            </Button>
                        )}
                    </Space>
                ),
            },
        ],
        [t, formatLocale, policy, currentUser?.id],
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
                    message={t('users.page.accessDeniedTitle')}
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
                        <Tooltip title={t('common.toolbar.refetchHint')}>
                            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                                {t('users.list.actionRefresh')}
                            </Button>
                        </Tooltip>
                        {policy.canCreate &&
                            (!isSuperAdminLayout || superAdminTab === 'platform') && (
                            <Button
                                type="primary"
                                icon={<UserOutlined />}
                                onClick={() => {
                                    setCreatePlatformMode(isSuperAdminLayout && superAdminTab === 'platform');
                                    setCreateOpen(true);
                                }}
                            >
                                {isSuperAdminLayout && superAdminTab === 'platform'
                                    ? t('users.page.createPlatformAdmin')
                                    : t('users.page.createUser')}
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
                    </Space>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                    {isSuperAdminLayout ? t('users.tabs.platform.description') : t('users.list.pageIntro')}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                    {t('users.list.forensicsHintLead')}{' '}
                    <Link href="/audit-logs">{t('users.list.forensicsLinkAuditLog')}</Link>
                    {' · '}
                    <Link href="/rksv/verifications">{t('users.list.forensicsLinkVerifications')}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            {isSuperAdminLayout ? (
                <Tabs
                    activeKey={superAdminTab}
                    onChange={(key) => setSuperAdminTab(key as 'platform' | 'tenant' | 'invitations')}
                    items={[
                        {
                            key: 'platform',
                            label: t('users.tabs.labelPlatform'),
                            children: (
                                <>
                                    <Card size="small" title={t('users.list.filterCardTitle')} style={{ marginBottom: 16 }}>
                                        <Flex wrap="wrap" gap="small" align="center">
                                            <Typography.Text type="secondary">
                                                {t('users.list.filterBandLabel')}
                                            </Typography.Text>
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
                                                onChange={(v) =>
                                                    setStatusFilter(v === undefined ? undefined : v === 'active')
                                                }
                                                options={statusFilterOptions}
                                            />
                                            <Button icon={<ClearOutlined />} onClick={resetAllFilters}>
                                                {t('users.list.clearAllFilters')}
                                            </Button>
                                        </Flex>
                                    </Card>
                                    {platformListIsError ? (
                                        <Alert
                                            type="error"
                                            showIcon
                                            message={t('users.list.errorLoad')}
                                            action={
                                                <Button size="small" onClick={() => platformListRefetch()}>
                                                    {t('users.list.retry')}
                                                </Button>
                                            }
                                        />
                                    ) : null}
                                    <PlatformUsersTab
                                        users={users}
                                        loading={platformListLoading}
                                        policy={policy}
                                        currentUserId={currentUser?.id}
                                        onView={setDetailUser}
                                        onEdit={(id) => setEditUserId(id)}
                                        onDeactivate={setDeactivateUserRecord}
                                        onReactivate={setReactivateUserRecord}
                                        onResetPassword={setResetPasswordUser}
                                    />
                                </>
                            ),
                        },
                        {
                            key: 'tenant',
                            label: t('users.tabs.labelTenant'),
                            children: (
                                <TenantUsersTab
                                    policy={policy}
                                    roleDisplayLabel={roleDisplayLabel}
                                    onEdit={(id) => setEditUserId(id)}
                                />
                            ),
                        },
                        {
                            key: 'invitations',
                            label: t('users.tabs.labelInvitations'),
                            children: <UserInvitationsPanel />,
                        },
                    ]}
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
                            message={t('users.list.errorLoad')}
                            description={
                                normalizeError(listError, t('users.list.errorLoadDetailFallback')).message ||
                                t('users.list.errorLoadDetailFallback')
                            }
                            action={
                                <Space direction="vertical" size="small">
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

                    <Table
                        columns={columns}
                        dataSource={users}
                        loading={isLoading}
                        rowKey={(r) => r.id ?? ''}
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
                        locale={{
                            emptyText: (
                                <Empty
                                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                                    description={
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
                            ),
                        }}
                    />
                </>
            )}

            <UserFormDrawer
                open={createOpen}
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
            <UserFormDrawer
                key={editUserId ?? 'edit'}
                open={!!editUserId}
                onClose={() => setEditUserId(null)}
                mode="edit"
                user={editUserFull ?? undefined}
                initialLoading={!!editUserId && editUserLoading}
                fetchError={editUserError ? (editUserFetchError ?? null) : null}
                onRetryFetch={refetchEditUser}
                roleOptions={roleOptions}
                rolesLoading={rolesLoading}
                onSubmit={handleEdit}
                loading={updateMutation.isPending}
            />

            <UserDetailDrawer
                open={!!detailUser}
                onClose={() => setDetailUser(null)}
                user={detailUser}
            />

            <Modal
                title={usersCopy.deactivateUser}
                open={!!deactivateUserRecord}
                onOk={handleDeactivate}
                onCancel={() => { setDeactivateUserRecord(null); deactivateForm.resetFields(); }}
                okText={usersCopy.okDeactivate}
                okButtonProps={{ danger: true }}
                confirmLoading={deactivateMutation.isPending}
                destroyOnHidden
            >
                {deactivateUserRecord && (
                    <p style={{ marginBottom: 16 }}>
                        <strong>{fullName(deactivateUserRecord)}</strong> ({deactivateUserRecord.email ?? deactivateUserRecord.userName}) {usersCopy.confirmDeactivate}
                    </p>
                )}
                <Form form={deactivateForm} layout="vertical">
                    <Form.Item name="reason" label={usersCopy.reasonRequired} rules={modalRules.reason}>
                        <Input.TextArea rows={3} placeholder={usersCopy.reasonPlaceholder} maxLength={500} showCount />
                    </Form.Item>
                </Form>
            </Modal>

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

            <Modal
                title={usersCopy.resetPasswordUser}
                open={!!resetPasswordUser}
                onOk={handleResetPassword}
                onCancel={() => { setResetPasswordUser(null); setResetPasswordValidationError(null); resetPasswordForm.resetFields(); }}
                okText={usersCopy.save}
                confirmLoading={resetPasswordMutation.isPending}
                destroyOnHidden
            >
                {resetPasswordUser && (
                    <>
                        <p style={{ marginBottom: 8 }}>
                            <strong>{fullName(resetPasswordUser)}</strong> ({resetPasswordUser.userName})
                        </p>
                        <Alert
                            type="info"
                            message={usersCopy.resetPasswordSecurityNote}
                            showIcon
                            style={{ marginBottom: 16 }}
                        />
                        {resetPasswordValidationError && (
                            <Alert
                                type="error"
                                message={resetPasswordValidationError}
                                showIcon
                                style={{ marginBottom: 16 }}
                            />
                        )}
                        <Form form={resetPasswordForm} layout="vertical">
                            <Form.Item name="newPassword" label={usersCopy.newPassword} rules={modalRules.newPassword}>
                                <Input.Password placeholder="••••••••" autoComplete="new-password" />
                            </Form.Item>
                        </Form>
                    </>
                )}
            </Modal>

            <Modal
                title={usersCopy.createRole}
                open={createRoleOpen}
                onOk={handleCreateRole}
                onCancel={() => { setCreateRoleOpen(false); createRoleForm.resetFields(); }}
                okText={usersCopy.save}
                confirmLoading={createRoleMutation.isPending}
                destroyOnHidden
            >
                <Form form={createRoleForm} layout="vertical">
                    <Form.Item name="name" label={usersCopy.roleName} rules={modalRules.roleName}>
                        <Input placeholder="z. B. Manager" maxLength={50} showCount autoComplete="off" />
                    </Form.Item>
                </Form>
            </Modal>

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
        </AdminPageShell>
    );
}
