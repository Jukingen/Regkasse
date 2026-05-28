'use client';

import React, { useMemo } from 'react';
import Link from 'next/link';
import { Button, Empty, Space, Table, Tag } from 'antd';
import type { ColumnsType, TableProps } from 'antd/es/table';
import {
    CheckCircleOutlined,
    EditOutlined,
    EyeOutlined,
    KeyOutlined,
    StopOutlined,
} from '@ant-design/icons';

import type { UserInfo } from '@/features/users/api/usersGateway';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';
import { isPlatformUserRole } from '@/features/users/utils/userScope';

function displayName(record: UserInfo): string {
    const name = `${record.firstName ?? ''} ${record.lastName ?? ''}`.trim();
    return name || '—';
}

export type UsersTableProps = {
    users: UserInfo[];
    loading?: boolean;
    policy: UsersPolicy;
    currentUserId?: string | null;
    onView: (user: UserInfo) => void;
    onEdit: (userId: string) => void;
    onDeactivate: (user: UserInfo) => void;
    onReactivate: (user: UserInfo) => void;
    onResetPassword: (user: UserInfo) => void;
    onUsernameEdit?: (user: UserInfo) => void;
    /** When set, renders a Details link to `{adminUserDetailsPath}/{userId}`. */
    adminUserDetailsPath?: string;
    pagination?: TableProps<UserInfo>['pagination'];
    virtual?: boolean;
    scroll?: TableProps<UserInfo>['scroll'];
    emptyDescription?: React.ReactNode;
};

export function UsersTable({
    users,
    loading,
    policy,
    currentUserId,
    onView,
    onEdit,
    onDeactivate,
    onReactivate,
    onResetPassword,
    onUsernameEdit,
    adminUserDetailsPath,
    pagination,
    virtual,
    scroll,
    emptyDescription,
}: UsersTableProps) {
    const { t, formatLocale } = useI18n();

    const columns: ColumnsType<UserInfo> = useMemo(
        () => [
            {
                title: t('users.list.columnUserName'),
                dataIndex: 'userName',
                key: 'userName',
                width: 150,
                ellipsis: true,
                render: (text: string | null | undefined) => text?.trim() || '—',
                sorter: (a, b) =>
                    (a.userName ?? '').localeCompare(b.userName ?? '', undefined, { sensitivity: 'base' }),
            },
            {
                title: t('users.list.columnEmail'),
                dataIndex: 'email',
                key: 'email',
                ellipsis: true,
                sorter: (a, b) => (a.email ?? '').localeCompare(b.email ?? '', undefined, { sensitivity: 'base' }),
                render: (v: string | null) => v ?? '—',
            },
            {
                title: t('users.list.columnName'),
                key: 'name',
                ellipsis: true,
                render: (_: unknown, record: UserInfo) => displayName(record),
                sorter: (a, b) => displayName(a).localeCompare(displayName(b), undefined, { sensitivity: 'base' }),
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
                    <Space wrap size="small">
                        {adminUserDetailsPath && record.id ? (
                            <Link href={`${adminUserDetailsPath}/${record.id}`}>
                                <Button type="link" size="small">
                                    {t('users.list.details')}
                                </Button>
                            </Link>
                        ) : null}
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
                        {policy.canEdit && onUsernameEdit && record.id ? (
                            <Button type="link" size="small" onClick={() => onUsernameEdit(record)}>
                                {t('users.username.editTitle')}
                            </Button>
                        ) : null}
                        {policy.canDeactivate && record.isActive && !isPlatformUserRole(record.role) && (
                            <Button
                                size="small"
                                danger
                                icon={<StopOutlined />}
                                onClick={() => onDeactivate(record)}
                            >
                                {t('users.list.deactivate')}
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
            onUsernameEdit,
            adminUserDetailsPath,
        ],
    );

    return (
        <Table
            rowKey={(r) => r.id ?? ''}
            loading={loading}
            dataSource={users}
            columns={columns}
            virtual={virtual}
            scroll={scroll}
            pagination={pagination}
            locale={{
                emptyText: (
                    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyDescription} />
                ),
            }}
        />
    );
}
