'use client';

import React, { useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Empty,
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
    ReloadOutlined,
    UserAddOutlined,
    UserDeleteOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { InviteUserModal } from '@/features/users/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/users/components/InviteUserModal';
import { UserCreatedSuccessModal } from '@/features/super-admin/components/UserCreatedSuccessModal';
import { createTenantUser, type CreateTenantUserResult } from '@/features/super-admin/api/tenantUsers';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import {
    adminUsersQueryKeys,
    listTenantUsers,
    removeUserFromTenant,
    tenantRowToTenantUser,
    type TenantUserRow,
} from '@/features/users/api/users';
import { UserRoleBadge } from '@/features/users/components/UserRoleBadge';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { isBusinessTenantSlug } from '@/features/users/utils/userScope';
import { useI18n } from '@/i18n';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';
const TENANT_ROLE_FILTER_VALUES = ['Manager', 'Cashier', 'Accountant'] as const;

export type TenantUsersTabProps = {
    policy: UsersPolicy;
    roleDisplayLabel: (role: string) => string;
    onEdit: (userId: string) => void;
};

/** Mandant users: tenant column, filters, invite, remove membership (not account delete). */
export function TenantUsersTab({ policy, roleDisplayLabel, onEdit }: TenantUsersTabProps) {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [tenantIdFilter, setTenantIdFilter] = useState<string | undefined>();
    const [roleFilter, setRoleFilter] = useState<string | undefined>();
    const [search, setSearch] = useState('');
    const [inviteOpen, setInviteOpen] = useState(false);
    const [createResult, setCreateResult] = useState<CreateTenantUserResult | null>(null);
    const [resetRow, setResetRow] = useState<TenantUserRow | null>(null);

    const tenantsQuery = useGetApiAdminTenants();
    const { tenants: inviteTenants, isLoading: inviteTenantsLoading } = useTenantList();
    const businessTenants = useMemo(
        () => (tenantsQuery.data ?? []).filter((row) => row.isActive && isBusinessTenantSlug(row.slug)),
        [tenantsQuery.data],
    );

    const tenantUsersQuery = useQuery({
        queryKey: adminUsersQueryKeys.tenant(tenantIdFilter, roleFilter),
        queryFn: () =>
            listTenantUsers({
                tenantId: tenantIdFilter,
                role: roleFilter || undefined,
                isActive: true,
            }),
        select: (data) => data.map(tenantRowToTenantUser),
    });

    const tenantFilterOptions = useMemo(
        () => [
            { value: '', label: t('users.tabs.tenant.filterAll') },
            ...businessTenants
                .map((tenant) => ({
                    value: tenant.id,
                    label: t('users.invite.tenantOption', { name: tenant.name, slug: tenant.slug }),
                }))
                .sort((a, b) => a.label.localeCompare(b.label)),
        ],
        [businessTenants, t],
    );

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
            message.success(t('tenants.users.invite.messages.created'));
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
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

    const rows = tenantUsersQuery.data ?? [];
    const filtered = useMemo(() => {
        const q = search.trim().toLowerCase();
        if (!q) return rows;
        return rows.filter((row) => {
            if (tenantIdFilter && row.tenantId !== tenantIdFilter) return false;
            if (roleFilter && row.role !== roleFilter) return false;
            return (
                row.name.toLowerCase().includes(q) ||
                row.email.toLowerCase().includes(q) ||
                row.tenantSlug.toLowerCase().includes(q) ||
                row.role.toLowerCase().includes(q)
            );
        });
    }, [rows, search, tenantIdFilter, roleFilter]);

    const columns: ColumnsType<TenantUserRow> = useMemo(
        () => [
            {
                title: t('users.tabs.tenant.columnTenant'),
                dataIndex: 'tenantSlug',
                key: 'tenantSlug',
                render: (slug: string, row) => (
                    <Space direction="vertical" size={0}>
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
                    <Space direction="vertical" size={0}>
                        <span style={{ fontWeight: 600 }}>{name}</span>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {row.email}
                        </Typography.Text>
                    </Space>
                ),
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
                        {policy.canEdit && (
                            <Button size="small" icon={<EditOutlined />} onClick={() => onEdit(row.userId)}>
                                {t('users.list.edit')}
                            </Button>
                        )}
                        {policy.canProvisionTenantCredentials && policy.canResetPassword(row.role) && (
                            <Button size="small" icon={<KeyOutlined />} onClick={() => setResetRow(row)}>
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
                                    removeMutation.mutate({ tenantId: row.tenantId, userId: row.userId })
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
                ),
            },
        ],
        [t, policy, removeMutation.isPending, onEdit],
    );

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('users.tabs.tenant.description')}
            </Typography.Paragraph>
            <Space wrap>
                <Button
                    icon={<ReloadOutlined />}
                    onClick={() => tenantUsersQuery.refetch()}
                    loading={tenantUsersQuery.isFetching}
                >
                    {t('common.buttons.refresh')}
                </Button>
                {policy.canProvisionTenantCredentials ? (
                    <Button type="primary" icon={<UserAddOutlined />} onClick={() => setInviteOpen(true)}>
                        {t('users.invite.action')}
                    </Button>
                ) : null}
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
            </Space>
            {tenantUsersQuery.isError ? (
                <Alert
                    type="error"
                    showIcon
                    message={t('users.list.errorLoad')}
                    action={
                        <Button size="small" onClick={() => tenantUsersQuery.refetch()}>
                            {t('users.list.retry')}
                        </Button>
                    }
                />
            ) : null}
            <Table
                rowKey={(r) => `${r.tenantId}:${r.userId}`}
                loading={tenantUsersQuery.isLoading}
                dataSource={filtered}
                columns={columns}
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
            <InviteUserModal
                open={inviteOpen}
                variant="usersPage"
                tenantRows={inviteTenants}
                tenantsLoading={inviteTenantsLoading}
                confirmLoading={createMutation.isPending}
                onClose={() => setInviteOpen(false)}
                onSubmit={(values) => createMutation.mutate(values)}
            />
            {policy.canProvisionTenantCredentials ? (
                <>
                    <UserCreatedSuccessModal
                        open={!!createResult}
                        result={createResult}
                        onClose={() => setCreateResult(null)}
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
