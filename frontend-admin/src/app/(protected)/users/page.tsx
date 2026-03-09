'use client';

/**
 * User Management – RKSV/DSGVO uyumlu kullanıcı yaşam döngüsü.
 * Tablo: name, email, role, branch, status, last login, actions.
 * Filtreler: role, status, branch, search. Drawer create/edit, deaktive (reason), reaktive, Activity timeline tab.
 */
import React, { useState, useMemo, useEffect } from 'react';
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
} from 'antd';
import {
    UserOutlined,
    EditOutlined,
    StopOutlined,
    CheckCircleOutlined,
    HistoryOutlined,
    EyeOutlined,
    SearchOutlined,
    KeyOutlined,
} from '@ant-design/icons';
import { useQueryClient, useMutation, useQuery } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { useRoles } from '@/features/users/hooks/useRoles';
import {
    listQueryKey,
    rolesQueryKey,
    createUser as gatewayCreateUser,
    updateUser as gatewayUpdateUser,
    getUserById,
    getUserByIdQueryKey,
    deactivateUser as gatewayDeactivateUser,
    reactivateUser as gatewayReactivateUser,
    resetPassword as gatewayResetPassword,
    createRole as gatewayCreateRole,
    normalizeError,
    type UserInfo,
    type CreateUserRequest,
    type UpdateUserRequest,
} from '@/features/users/api/usersGateway';
import { UserDetailDrawer } from '@/features/users/components/UserDetailDrawer';
import { UserFormDrawer } from '@/features/users/components/UserFormDrawer';
import { usersCopy, mapBackendPasswordErrorToGerman } from '@/features/users/constants/copy';
import { createUsersFormRules } from '@/features/users/constants/validation';

const { Title } = Typography;

function fullName(record: UserInfo): string {
    const first = record.firstName ?? '';
    const last = record.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || record.userName || record.id || '—';
}

const ROLE_OPTIONS = [
    { value: 'SuperAdmin', label: 'SuperAdmin' },
    { value: 'Admin', label: 'Admin' },
    { value: 'BranchManager', label: 'BranchManager' },
    { value: 'Auditor', label: 'Auditor' },
];

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
    const { data: listData, isLoading, isError, refetch } = useUsersList(listParams, { enabled: policy.canView });
    const users = listData?.items ?? [];
    const pagination = listData?.pagination;
    useEffect(() => {
        setPage(DEFAULT_PAGE);
    }, [roleFilter, statusFilter, searchTerm]);
    const { data: roles } = useRoles({ enabled: policy.canView });
    // Edit flow: when editUserId is set, fetch full user detail (GET /api/UserManagement/{id}); form is filled from this response only.
    const { data: editUserFull, isLoading: editUserLoading, isError: editUserError, error: editUserFetchError, refetch: refetchEditUser } = useQuery({
        queryKey: getUserByIdQueryKey(editUserId ?? ''),
        queryFn: () => getUserById(editUserId!),
        enabled: !!editUserId,
    });
    const modalRules = useMemo(() => createUsersFormRules(modalFormRulesContext), []);

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
        onSuccess: () => {
            message.success(usersCopy.successUpdate);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
            setEditUserId(null);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const resetPasswordMutation = useMutation({
        mutationFn: ({ id, data }: { id: string; data: { newPassword: string } }) => gatewayResetPassword(id, data),
        onSuccess: () => {
            message.success(usersCopy.successResetPassword);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
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
        onSuccess: () => {
            message.success(usersCopy.successDeactivate);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
            setDeactivateUserRecord(null);
            deactivateForm.resetFields();
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });
    const reactivateMutation = useMutation({
        mutationFn: (id: string) => gatewayReactivateUser(id),
        onSuccess: () => {
            message.success(usersCopy.successReactivate);
            queryClient.invalidateQueries({ queryKey: listQueryKey });
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
            setCreateRoleOpen(false);
            createRoleForm.resetFields();
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
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
                    <Button
                        size="small"
                        icon={<HistoryOutlined />}
                        onClick={() => setDetailUser(record)}
                    >
                        {usersCopy.activity}
                    </Button>
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
            <Card>
                <Alert
                    type="warning"
                    message={usersCopy.accessDenied}
                    description="Nur Rollen mit UsersView (SuperAdmin, Admin, Administrator, BranchManager, Auditor) können diese Seite öffnen."
                />
            </Card>
        );
    }

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24, flexWrap: 'wrap', gap: 16 }}>
                <Title level={3} style={{ margin: 0 }}>{usersCopy.title}</Title>
                <Space wrap>
                    <Input.Search
                        placeholder={usersCopy.searchPlaceholder}
                        allowClear
                        value={searchInput}
                        onChange={(e) => setSearchInput(e.target.value)}
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
                        options={ROLE_OPTIONS}
                    />
                    <Select
                        placeholder={usersCopy.filterStatus}
                        allowClear
                        style={{ width: 120 }}
                        value={statusFilter === undefined ? undefined : statusFilter ? 'active' : 'inactive'}
                        onChange={(v) => setStatusFilter(v === undefined ? undefined : v === 'active')}
                        options={STATUS_OPTIONS}
                    />
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
                </Space>
            </div>

            {isError && (
                <Alert
                    type="error"
                    message={usersCopy.errorLoad}
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            {usersCopy.retry}
                        </Button>
                    }
                    style={{ marginBottom: 16 }}
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
                    onChange: (newPage, newPageSize) => {
                        setPage(newPage);
                        if (newPageSize != null) setPageSize(newPageSize);
                    },
                }}
                locale={{
                    emptyText: (
                        <Empty
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                            description={usersCopy.emptyList}
                        />
                    ),
                }}
            />

            <UserFormDrawer
                open={createOpen}
                onClose={() => setCreateOpen(false)}
                mode="create"
                roleOptions={roleOptions}
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
                destroyOnClose
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
                destroyOnClose
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
                destroyOnClose
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
                destroyOnClose
            >
                <Form form={createRoleForm} layout="vertical">
                    <Form.Item name="name" label={usersCopy.roleName} rules={modalRules.roleName}>
                        <Input placeholder="z. B. Manager" maxLength={50} showCount autoComplete="off" />
                    </Form.Item>
                </Form>
            </Modal>
        </Card>
    );
}
