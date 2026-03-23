'use client';

/**
 * User Management – RKSV/DSGVO uyumlu kullanıcı yaşam döngüsü.
 * Tablo: name, email, role, branch, status, last login, actions.
 * Filtreler: role, status, branch, search. Drawer create/edit, deaktive (reason), reaktive, Activity timeline tab.
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
} from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
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
import { OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';
import { useQueryClient, useMutation, useQuery } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { useRoles } from '@/features/users/hooks/useRoles';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
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

const ROLE_OPTIONS = CANONICAL_ROLE_VALUES.map((value) => ({
    value,
    label: usersCopy.roleDisplayName(value),
}));

const STATUS_OPTIONS = [
    { value: 'active', label: usersCopy.statusActive },
    { value: 'inactive', label: usersCopy.statusInactive },
];

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

export default function UsersPage() {
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

    const { user: currentUser } = useAuth();
    const policy = useUsersPolicy();

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
    const { data: listData, isLoading, isFetching, isError, error: listError, refetch } = useUsersList(listParams, {
        enabled: policy.canView,
    });
    const users = listData?.items ?? [];
    const pagination = listData?.pagination;
    useEffect(() => {
        setPage(DEFAULT_PAGE);
    }, [roleFilter, statusFilter, searchTerm]);

    const usersScopeSummary = useMemo(() => {
        const parts: string[] = [
            `Seite ${pagination?.page ?? page}`,
            `${pagination?.pageSize ?? pageSize} pro Seite`,
            pagination?.totalCount != null
                ? `${pagination.totalCount.toLocaleString('de-DE')} gesamt (API)`
                : usersCopy.scopeTotalLoading,
        ];
        if (searchTerm.trim()) {
            parts.push(`${usersCopy.scopeSearchPrefix} «${searchTerm.trim()}»`);
        }
        if (roleFilter) {
            parts.push(`${usersCopy.scopeRolePrefix} = ${roleFilter}`);
        }
        if (statusFilter === undefined) {
            parts.push(usersCopy.scopeStatusAll);
        } else {
            parts.push(
                `${usersCopy.scopeStatusPrefix}: ${statusFilter ? usersCopy.statusActive : usersCopy.statusInactive}`,
            );
        }
        return parts.join(' · ');
    }, [pagination, page, pageSize, searchTerm, roleFilter, statusFilter]);

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
        () => (roles?.map((r) => ({ value: r, label: r })) ?? ROLE_OPTIONS),
        [roles]
    );

    const [deactivateForm] = Form.useForm();
    const [resetPasswordForm] = Form.useForm();
    const [createRoleForm] = Form.useForm<{ name: string }>();

    const createMutation = useMutation({
        mutationFn: gatewayCreateUser,
        onSuccess: () => {
            message.success(usersCopy.successCreate);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
            setCreateOpen(false);
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
            message.success(usersCopy.successCreateRole ?? 'Rolle angelegt.');
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
        };
        createMutation.mutate(createPayload);
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

    const columns = [
        {
            title: usersCopy.name,
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
        { title: usersCopy.email, dataIndex: 'email', key: 'email', ellipsis: true, render: (v: string | null) => v ?? '—' },
        { title: usersCopy.role, dataIndex: 'role', key: 'role', render: (role: string) => <Tag color="gold">{role ?? '—'}</Tag> },
        { title: usersCopy.branch, key: 'branch', render: () => usersCopy.branchNotAvailable },
        {
            title: usersCopy.status,
            dataIndex: 'isActive',
            key: 'status',
            render: (active: boolean) => (
                <Tag color={active ? 'green' : 'red'}>{active ? usersCopy.statusActive : usersCopy.statusInactive}</Tag>
            ),
        },
        {
            title: usersCopy.lastLogin,
            dataIndex: 'lastLoginAt',
            key: 'lastLoginAt',
            render: (v: string | null) => (v ? new Date(v).toLocaleString('de-DE') : '—'),
        },
        {
            title: usersCopy.actions,
            key: 'actions',
            render: (_: unknown, record: UserInfo) => (
                <Space wrap>
                    {policy.canEdit && (
                        <Button
                            size="small"
                            icon={<EyeOutlined />}
                            onClick={() => setDetailUser(record)}
                        >
                            {usersCopy.view}
                        </Button>
                    )}
                    {policy.canEdit && (
                        <Button
                            size="small"
                            icon={<EditOutlined />}
                            onClick={() => setEditUserId(record.id ?? null)}
                        >
                            {usersCopy.edit}
                        </Button>
                    )}
                    {policy.canDeactivate && record.isActive && (
                        <Button
                            size="small"
                            danger
                            icon={<StopOutlined />}
                            onClick={() => setDeactivateUserRecord(record)}
                        >
                            {usersCopy.deactivate}
                        </Button>
                    )}
                    {policy.canReactivate && !record.isActive && (
                        <Button
                            size="small"
                            type="primary"
                            icon={<CheckCircleOutlined />}
                            onClick={() => setReactivateUserRecord(record)}
                        >
                            {usersCopy.reactivate}
                        </Button>
                    )}
                    {policy.canResetPassword(record.role) && record.id !== currentUser?.id && (
                        <Button
                            size="small"
                            icon={<KeyOutlined />}
                            onClick={() => setResetPasswordUser(record)}
                        >
                            {usersCopy.resetPassword}
                        </Button>
                    )}
                </Space>
            ),
        },
    ];

    if (!policy.canView) {
        return (
            <AdminPageShell>
                <AdminPageHeader
                    title={usersCopy.title}
                    breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: usersCopy.title }]}
                />
                <Alert
                    type="warning"
                    showIcon
                    message={usersCopy.accessDenied}
                    description="Nur mit Berechtigung „Benutzer anzeigen“ (z. B. SuperAdmin, Manager) können Sie diese Seite öffnen."
                />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={usersCopy.title}
                breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: usersCopy.title }]}
                actions={
                    <Space wrap>
                        <Tooltip title={OPERATOR_SHARED_COPY.refetchHintToolbar}>
                            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                                {usersCopy.actionRefresh}
                            </Button>
                        </Tooltip>
                        {policy.canCreate && (
                            <Button type="primary" icon={<UserOutlined />} onClick={() => setCreateOpen(true)}>
                                {usersCopy.createUser}
                            </Button>
                        )}
                        {policy.canCreateRole && (
                            <Button icon={<UserOutlined />} onClick={() => setCreateRoleOpen(true)}>
                                {usersCopy.createRole}
                            </Button>
                        )}
                        {canManageRoles && (
                            <Button type="default" onClick={() => setRoleManagementDrawerOpen(true)}>
                                {usersCopy.manageRoles}
                            </Button>
                        )}
                    </Space>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                    {usersCopy.pageIntro}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                    {usersCopy.forensicsHintLead}{' '}
                    <Link href="/audit-logs">{usersCopy.forensicsLinkAuditLog}</Link>
                    {' · '}
                    <Link href="/rksv/verifications">{usersCopy.forensicsLinkVerifications}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            <Card size="small" title={usersCopy.filterCardTitle}>
                <Flex wrap="wrap" gap="small" align="center">
                    <Typography.Text type="secondary">{usersCopy.filterBandLabel}</Typography.Text>
                    <Input.Search
                        placeholder={usersCopy.searchPlaceholder}
                        allowClear
                        value={searchInput}
                        onChange={(e) => {
                            const v = e.target.value;
                            setSearchInput(v);
                            // Keep list query in sync when the field is cleared (X or delete), same pattern as Customers.
                            if (!v) setSearchTerm('');
                        }}
                        onSearch={(v) => setSearchTerm(v ?? '')}
                        style={{ width: 260 }}
                        enterButton={<SearchOutlined />}
                    />
                    <Select
                        placeholder={usersCopy.filterRole}
                        allowClear
                        style={{ width: 140 }}
                        value={roleFilter}
                        onChange={setRoleFilter}
                        options={roleOptions}
                    />
                    <Select
                        placeholder={usersCopy.filterStatus}
                        allowClear
                        style={{ width: 120 }}
                        value={statusFilter === undefined ? undefined : statusFilter ? 'active' : 'inactive'}
                        onChange={(v) => setStatusFilter(v === undefined ? undefined : v === 'active')}
                        options={STATUS_OPTIONS}
                    />
                    <Button icon={<ClearOutlined />} onClick={resetAllFilters}>
                        {usersCopy.clearAllFilters}
                    </Button>
                </Flex>
            </Card>

            {hasNonDefaultListFilters ? (
                <div style={{ marginTop: 4 }}>
                    <Space wrap size={[8, 8]} align="center">
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {usersCopy.activeFiltersLabel}
                        </Typography.Text>
                        {searchTerm.trim() ? (
                            <Tag
                                closable
                                onClose={() => {
                                    setSearchInput('');
                                    setSearchTerm('');
                                }}
                            >
                                {usersCopy.scopeSearchPrefix}: «{searchTerm.trim()}»
                            </Tag>
                        ) : null}
                        {roleFilter ? (
                            <Tag closable onClose={() => setRoleFilter(undefined)}>
                                {usersCopy.scopeRolePrefix}: {usersCopy.roleDisplayName(roleFilter)}
                            </Tag>
                        ) : null}
                        <Tag
                            closable
                            onClose={() => setStatusFilter(true)}
                            color={statusFilter === undefined ? 'purple' : statusFilter ? 'green' : 'red'}
                        >
                            {usersCopy.scopeStatusPrefix}:{' '}
                            {statusFilter === undefined
                                ? usersCopy.statusAll
                                : statusFilter
                                  ? usersCopy.statusActive
                                  : usersCopy.statusInactive}
                        </Tag>
                        <Button type="link" size="small" onClick={resetAllFilters}>
                            {usersCopy.clearAllFilters}
                        </Button>
                    </Space>
                </div>
            ) : null}

            <AdminPageScopeSummary label={usersCopy.scopeSummaryLabel}>
                {usersScopeSummary}
                {isFetching && !isLoading && !isError ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {' '}
                        ({usersCopy.listRefreshingHint})
                    </Typography.Text>
                ) : null}
            </AdminPageScopeSummary>

            {isError && (
                <Alert
                    type="error"
                    showIcon
                    message={usersCopy.errorLoad}
                    description={
                        normalizeError(listError, usersCopy.errorLoadDetailFallback).message ||
                        usersCopy.errorLoadDetailFallback
                    }
                    action={
                        <Space direction="vertical" size="small">
                            <Button size="small" onClick={() => refetch()}>
                                {usersCopy.retry}
                            </Button>
                            <Button size="small" type="link" onClick={resetAllFilters} style={{ padding: 0, height: 'auto' }}>
                                {usersCopy.clearAllFilters}
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
                        if (total <= 0) return usersCopy.paginationZeroResults;
                        const from = range[0] ?? 0;
                        const to = range[1] ?? 0;
                        return `${from.toLocaleString('de-DE')}–${to.toLocaleString('de-DE')} von ${total.toLocaleString('de-DE')}`;
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
                                            ? usersCopy.emptyListWithFilters
                                            : usersCopy.emptyList}
                                    </Typography.Paragraph>
                                    {!hasNonDefaultListFilters ? (
                                        <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0 }}>
                                            {usersCopy.emptyListDefaultHint}
                                        </Typography.Paragraph>
                                    ) : null}
                                </div>
                            }
                        />
                    ),
                }}
            />

            <UserFormDrawer
                open={createOpen}
                onClose={() => setCreateOpen(false)}
                mode="create"
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
