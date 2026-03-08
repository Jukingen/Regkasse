'use client';

/**
 * User Management – RKSV/DSGVO uyumlu kullanıcı yaşam döngüsü.
 * Tablo: name, email, role, branch, status, last login, actions.
 * Filtreler: role, status, branch, search. Drawer create/edit, deaktive (reason), reaktive, Activity timeline tab.
 */
import React, { useState, useMemo } from 'react';
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
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { deactivateUser, reactivateUser } from '@/features/users/api/usersApi';
import {
    useGetApiUserManagementRoles,
    usePostApiUserManagement,
    usePutApiUserManagementId,
    usePutApiUserManagementIdResetPassword,
} from '@/api/generated/user-management/user-management';
import { UserDetailDrawer } from '@/features/users/components/UserDetailDrawer';
import { UserFormDrawer } from '@/features/users/components/UserFormDrawer';
import type { UserInfo } from '@/api/generated/model';
import type { CreateUserRequest, UpdateUserRequest } from '@/api/generated/model';
import { usersListQueryKey } from '@/features/users/hooks/useUsersList';
import { usersCopy } from '@/features/users/constants/copy';

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

export default function UsersPage() {
    const [roleFilter, setRoleFilter] = useState<string | undefined>();
    const [statusFilter, setStatusFilter] = useState<boolean | undefined>(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [searchInput, setSearchInput] = useState('');

    const [createOpen, setCreateOpen] = useState(false);
    const [editUser, setEditUser] = useState<UserInfo | null>(null);
    const [detailUser, setDetailUser] = useState<UserInfo | null>(null);
    const [deactivateUserRecord, setDeactivateUserRecord] = useState<UserInfo | null>(null);
    const [reactivateUserRecord, setReactivateUserRecord] = useState<UserInfo | null>(null);
    const [resetPasswordUser, setResetPasswordUser] = useState<UserInfo | null>(null);

    const { user: currentUser } = useAuth();
    const canManageUsers = ['SuperAdmin', 'Admin', 'BranchManager'].includes(currentUser?.role ?? '');

    const queryClient = useQueryClient();
    const listParams = useMemo(
        () => ({
            role: roleFilter,
            isActive: statusFilter,
            query: searchTerm.trim() || undefined,
        }),
        [roleFilter, statusFilter, searchTerm]
    );
    const { data: users, isLoading, isError, refetch } = useUsersList(listParams);
    const { data: roles } = useGetApiUserManagementRoles();

    const roleOptions = useMemo(
        () => (roles?.map((r) => ({ value: r, label: r })) ?? ROLE_OPTIONS),
        [roles]
    );

    const createMutation = usePostApiUserManagement({
        mutation: {
            onSuccess: () => {
                message.success(usersCopy.successCreate);
                queryClient.invalidateQueries({ queryKey: usersListQueryKey });
                setCreateOpen(false);
            },
            onError: (e: unknown) => {
                message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? usersCopy.errorGeneric);
            },
        },
    });
    const updateMutation = usePutApiUserManagementId({
        mutation: {
            onSuccess: () => {
                message.success(usersCopy.successUpdate);
                queryClient.invalidateQueries({ queryKey: usersListQueryKey });
                setEditUser(null);
            },
            onError: (e: unknown) => {
                message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? usersCopy.errorGeneric);
            },
        },
    });
    const resetPasswordMutation = usePutApiUserManagementIdResetPassword({
        mutation: {
            onSuccess: () => {
                message.success(usersCopy.successResetPassword);
                queryClient.invalidateQueries({ queryKey: usersListQueryKey });
                setResetPasswordUser(null);
                resetPasswordForm.resetFields();
            },
            onError: (e: unknown) => {
                message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? usersCopy.errorGeneric);
            },
        },
    });
    const [deactivateForm] = Form.useForm();
    const [resetPasswordForm] = Form.useForm();

    const handleCreate = (values: CreateUserRequest) => {
        createMutation.mutate({ data: values });
    };
    const handleEdit = (values: UpdateUserRequest) => {
        if (!editUser?.id) return;
        updateMutation.mutate({ id: editUser.id, data: values });
    };
    const handleDeactivate = () => {
        if (!deactivateUserRecord?.id) return;
        deactivateForm.validateFields().then((values: { reason: string }) => {
            deactivateUser(deactivateUserRecord.id!, { reason: values.reason })
                .then(() => {
                    message.success(usersCopy.successDeactivate);
                    queryClient.invalidateQueries({ queryKey: usersListQueryKey });
                    setDeactivateUserRecord(null);
                    deactivateForm.resetFields();
                })
                .catch((e: unknown) => {
                    message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? usersCopy.errorGeneric);
                });
        });
    };
    const handleReactivate = () => {
        if (!reactivateUserRecord?.id) return;
        reactivateUser(reactivateUserRecord.id!)
            .then(() => {
                message.success(usersCopy.successReactivate);
                queryClient.invalidateQueries({ queryKey: usersListQueryKey });
                setReactivateUserRecord(null);
            })
            .catch((e: unknown) => {
                message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? usersCopy.errorGeneric);
            });
    };
    const handleResetPassword = () => {
        if (!resetPasswordUser?.id) return;
        resetPasswordForm.validateFields().then((values: { newPassword: string }) => {
            resetPasswordMutation.mutate({ id: resetPasswordUser.id!, data: { newPassword: values.newPassword } });
        });
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
                    {canManageUsers && (
                        <Button
                            size="small"
                            icon={<EyeOutlined />}
                            onClick={() => setDetailUser(record)}
                        >
                            {usersCopy.view}
                        </Button>
                    )}
                    {canManageUsers && (
                        <Button
                            size="small"
                            icon={<EditOutlined />}
                            onClick={() => {
                                setEditUser(record);
                                // form set in UserFormDrawer via useEffect
                            }}
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
                    {canManageUsers && record.isActive && (
                        <Button
                            size="small"
                            danger
                            icon={<StopOutlined />}
                            onClick={() => setDeactivateUserRecord(record)}
                        >
                            {usersCopy.deactivate}
                        </Button>
                    )}
                    {canManageUsers && !record.isActive && (
                        <Button
                            size="small"
                            type="primary"
                            icon={<CheckCircleOutlined />}
                            onClick={() => setReactivateUserRecord(record)}
                        >
                            {usersCopy.reactivate}
                        </Button>
                    )}
                    {canManageUsers && (
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
                    {canManageUsers && (
                        <Button type="primary" icon={<UserOutlined />} onClick={() => setCreateOpen(true)}>
                            {usersCopy.createUser}
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
                dataSource={users ?? []}
                loading={isLoading}
                rowKey={(r) => r.id ?? ''}
                pagination={{ pageSize: 20, showSizeChanger: true }}
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
                open={!!editUser}
                onClose={() => setEditUser(null)}
                mode="edit"
                user={editUser}
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
                destroyOnClose
            >
                {deactivateUserRecord && (
                    <p style={{ marginBottom: 16 }}>
                        <strong>{fullName(deactivateUserRecord)}</strong> ({deactivateUserRecord.email ?? deactivateUserRecord.userName}) {usersCopy.confirmDeactivate}
                    </p>
                )}
                <Form form={deactivateForm} layout="vertical">
                    <Form.Item
                        name="reason"
                        label={usersCopy.reasonRequired}
                        rules={[{ required: true, message: usersCopy.reasonRequiredMessage }]}
                    >
                        <Input.TextArea rows={3} placeholder={usersCopy.reasonPlaceholder} />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title={usersCopy.reactivateUser}
                open={!!reactivateUserRecord}
                onOk={handleReactivate}
                onCancel={() => setReactivateUserRecord(null)}
                okText={usersCopy.okReactivate}
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
                onCancel={() => { setResetPasswordUser(null); resetPasswordForm.resetFields(); }}
                okText={usersCopy.save}
                confirmLoading={resetPasswordMutation.isPending}
                destroyOnClose
            >
                {resetPasswordUser && (
                    <>
                        <p style={{ marginBottom: 16 }}>
                            <strong>{fullName(resetPasswordUser)}</strong> ({resetPasswordUser.userName}) – neues Passwort setzen. Alle Sitzungen werden ungültig.
                        </p>
                        <Form form={resetPasswordForm} layout="vertical">
                            <Form.Item
                                name="newPassword"
                                label={usersCopy.newPassword}
                                rules={[{ required: true, message: usersCopy.newPassword }, { min: 6, message: 'Min. 6 Zeichen' }]}
                            >
                                <Input.Password placeholder="••••••••" autoComplete="new-password" />
                            </Form.Item>
                        </Form>
                    </>
                )}
            </Modal>
        </Card>
    );
}
