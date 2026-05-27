'use client';

import { Button, Popconfirm, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CrownOutlined, KeyOutlined, UserDeleteOutlined } from '@ant-design/icons';

import type { TenantUser } from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

const ASSIGNABLE_ROLES = ['Manager', 'Cashier', 'Waiter', 'Kitchen', 'ReportViewer', 'Accountant'] as const;

export type TenantUserTableProps = {
    users: TenantUser[];
    loading?: boolean;
    setOwnerPending?: boolean;
    removePending?: boolean;
    roleChangeUserId?: string | null;
    resetPendingUserId?: string | null;
    onSetOwner: (userId: string) => void;
    onRemove: (userId: string) => void;
    onRoleChange: (userId: string, role: string) => void;
    onResetPassword?: (userId: string) => void;
};

export function TenantUserTable({
    users,
    loading,
    setOwnerPending,
    removePending,
    roleChangeUserId,
    resetPendingUserId,
    onSetOwner,
    onRemove,
    onRoleChange,
    onResetPassword,
}: TenantUserTableProps) {
    const { t, formatLocale } = useI18n();

    const columns: ColumnsType<TenantUser> = [
        {
            title: t('tenants.users.columns.name'),
            dataIndex: 'name',
            key: 'name',
            render: (name: string, row) => (
                <Space>
                    <span>{name}</span>
                    {row.isOwner ? (
                        <Tag icon={<CrownOutlined />} color="gold">
                            {t('tenants.users.ownerBadge')}
                        </Tag>
                    ) : null}
                </Space>
            ),
        },
        {
            title: t('tenants.users.columns.userName'),
            dataIndex: 'userName',
            key: 'userName',
            width: 140,
            ellipsis: true,
            render: (userName: string) => userName?.trim() || '—',
            sorter: (a, b) =>
                (a.userName ?? '').localeCompare(b.userName ?? '', undefined, { sensitivity: 'base' }),
        },
        { title: t('tenants.users.columns.email'), dataIndex: 'email', key: 'email' },
        {
            title: t('tenants.users.columns.role'),
            dataIndex: 'role',
            key: 'role',
            render: (role: string, row) => (
                <Select
                    size="small"
                    style={{ minWidth: 140 }}
                    value={role}
                    loading={roleChangeUserId === row.userId}
                    onChange={(value) => onRoleChange(row.userId, value)}
                    options={ASSIGNABLE_ROLES.map((r) => ({ value: r, label: r }))}
                />
            ),
        },
        {
            title: t('tenants.users.columns.status'),
            key: 'status',
            render: () => <Tag color="green">{t('tenants.users.status.active')}</Tag>,
        },
        {
            title: t('tenants.users.columns.joined'),
            dataIndex: 'joinedAtUtc',
            key: 'joinedAtUtc',
            render: (v: string) => formatDateTime(v, formatLocale),
        },
        {
            title: t('tenants.users.columns.actions'),
            key: 'actions',
            render: (_, row) => (
                <Space size="small" wrap>
                    {onResetPassword ? (
                        <Button
                            size="small"
                            icon={<KeyOutlined />}
                            loading={resetPendingUserId === row.userId}
                            onClick={() => onResetPassword(row.userId)}
                        >
                            {t('tenants.users.actions.resetPassword')}
                        </Button>
                    ) : null}
                    {!row.isOwner && (
                        <Button
                            size="small"
                            icon={<CrownOutlined />}
                            loading={setOwnerPending}
                            onClick={() => onSetOwner(row.userId)}
                        >
                            {t('tenants.users.actions.setOwner')}
                        </Button>
                    )}
                    <Popconfirm
                        title={t('tenants.users.confirmRemove.title')}
                        description={t('tenants.users.confirmRemove.body')}
                        onConfirm={() => onRemove(row.userId)}
                    >
                        <Button size="small" danger icon={<UserDeleteOutlined />} loading={removePending}>
                            {t('tenants.users.actions.remove')}
                        </Button>
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <Table
            rowKey="userId"
            loading={loading}
            dataSource={users}
            columns={columns}
            locale={{ emptyText: t('tenants.users.empty') }}
            pagination={{ pageSize: 20 }}
        />
    );
}

export { ASSIGNABLE_ROLES };
