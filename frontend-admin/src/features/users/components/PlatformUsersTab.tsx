'use client';

import React, { useMemo } from 'react';
import {
    Avatar,
    Button,
    Empty,
    Space,
    Table,
    Tag,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    CheckCircleOutlined,
    EditOutlined,
    EyeOutlined,
    KeyOutlined,
    StopOutlined,
    UserAddOutlined,
    UserOutlined,
} from '@ant-design/icons';

import type { UserInfo } from '@/features/users/api/usersGateway';
import { UserRoleBadge } from '@/features/users/components/UserRoleBadge';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';

function fullName(record: UserInfo): string {
    const first = record.firstName ?? '';
    const last = record.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || record.userName || record.id || '—';
}

export type PlatformUsersTabProps = {
    users: UserInfo[];
    loading?: boolean;
    policy: UsersPolicy;
    currentUserId?: string | null;
    onView: (user: UserInfo) => void;
    onEdit: (userId: string) => void;
    onDeactivate: (user: UserInfo) => void;
    onReactivate: (user: UserInfo) => void;
    onResetPassword: (user: UserInfo) => void;
    onCreatePlatformUser?: () => void;
};

/** Platform operators: edit, disable account, reset password — no tenant membership actions. */
export function PlatformUsersTab({
    users,
    loading,
    policy,
    currentUserId,
    onView,
    onEdit,
    onDeactivate,
    onReactivate,
    onResetPassword,
    onCreatePlatformUser,
}: PlatformUsersTabProps) {
    const { t, formatLocale } = useI18n();

    const columns: ColumnsType<UserInfo> = useMemo(
        () => [
            {
                title: t('users.list.columnName'),
                key: 'user',
                render: (_: unknown, record: UserInfo) => (
                    <Space>
                        <Avatar icon={<UserOutlined />} />
                        <div>
                            <div style={{ fontWeight: 'bold' }}>{fullName(record)}</div>
                            <div style={{ fontSize: 12, color: '#999' }}>
                                {record.email ?? record.userName ?? '—'}
                            </div>
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
                render: (role: string) => (
                    <Space size="small">
                        <UserRoleBadge role={role} platform />
                    </Space>
                ),
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
                    <Space wrap size="small">
                        {policy.canEdit && (
                            <Button size="small" icon={<EyeOutlined />} onClick={() => onView(record)}>
                                {t('users.list.view')}
                            </Button>
                        )}
                        {policy.canEdit && (
                            <Button size="small" icon={<EditOutlined />} onClick={() => onEdit(record.id ?? '')}>
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
                ),
            },
        ],
        [t, formatLocale, policy, currentUserId, onView, onEdit, onDeactivate, onReactivate, onResetPassword],
    );

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('users.tabs.platform.description')}
            </Typography.Paragraph>
            {policy.canCreate && onCreatePlatformUser ? (
                <Button type="primary" icon={<UserAddOutlined />} onClick={onCreatePlatformUser}>
                    {t('users.page.createPlatformAdmin')}
                </Button>
            ) : null}
            <Table
                rowKey={(r) => r.id ?? ''}
                loading={loading}
                dataSource={users}
                columns={columns}
                pagination={{ pageSize: 20, showSizeChanger: true, pageSizeOptions: [10, 20, 50] }}
                locale={{
                    emptyText: (
                        <Empty
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                            description={t('users.tabs.platform.empty')}
                        />
                    ),
                }}
            />
        </Space>
    );
}
