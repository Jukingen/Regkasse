'use client';

import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Avatar,
    Button,
    Card,
    Input,
    Select,
    Space,
    Table,
    Tag,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined, ReloadOutlined } from '@ant-design/icons';
import Link from 'next/link';

import { useStaffList } from '@/features/staff/hooks/useStaffList';
import { UserDetailDrawer } from '@/features/users/components/UserDetailDrawer';
import type { UserInfo } from '@/features/users/api/usersGateway';
import { useRoles } from '@/features/users/hooks/useRoles';
import { useStaffPolicy } from '@/shared/auth/staffPolicy';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

const CANONICAL_ROLE_VALUES = [
    'SuperAdmin',
    'Manager',
    'Cashier',
    'Waiter',
    'Kitchen',
    'ReportViewer',
    'Accountant',
] as const;

function isCanonicalRoleName(roleName: string): roleName is (typeof CANONICAL_ROLE_VALUES)[number] {
    return (CANONICAL_ROLE_VALUES as readonly string[]).includes(roleName);
}

function displayName(record: UserInfo): string {
    const name = `${record.firstName ?? ''} ${record.lastName ?? ''}`.trim();
    return name || record.userName || record.id || '—';
}

function initials(record: UserInfo): string {
    const name = displayName(record);
    return name.charAt(0).toUpperCase() || '?';
}

export function StaffList() {
    const { t, formatLocale } = useI18n();
    const staffPolicy = useStaffPolicy();
    const canManageUsers = staffPolicy.canManage;

    const [searchInput, setSearchInput] = useState('');
    const [detailUser, setDetailUser] = useState<UserInfo | null>(null);

    const {
        staff,
        pagination,
        isLoading,
        isFetching,
        isPlaceholderData,
        refetch,
        page,
        pageSize,
        filters,
        setPage,
        setPageSize,
        setSearch,
        setRoleFilter,
        setStatusFilter,
    } = useStaffList({ enabled: staffPolicy.canView });

    const { data: roles = [] } = useRoles({ enabled: staffPolicy.canView });

    const roleDisplayLabel = useCallback(
        (roleName: string) =>
            isCanonicalRoleName(roleName) ? t(`users.roles.displayNames.${roleName}`) : roleName,
        [t],
    );

    const columns: ColumnsType<UserInfo> = useMemo(
        () => [
            {
                title: t('staff:list.columnStaff'),
                key: 'name',
                render: (_: unknown, record) => (
                    <Space>
                        <Avatar>{initials(record)}</Avatar>
                        <div>
                            <Typography.Text strong>{displayName(record)}</Typography.Text>
                            <div>
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {record.email ?? record.userName ?? '—'}
                                </Typography.Text>
                            </div>
                        </div>
                    </Space>
                ),
            },
            {
                title: t('staff:list.columnRole'),
                dataIndex: 'role',
                key: 'role',
                render: (role: string | undefined) => (
                    <Tag color="blue">{role ? roleDisplayLabel(role) : '—'}</Tag>
                ),
            },
            {
                title: t('staff:list.columnStatus'),
                dataIndex: 'isActive',
                key: 'isActive',
                render: (isActive: boolean) => (
                    <Tag color={isActive ? 'green' : 'red'}>
                        {isActive ? t('staff:list.active') : t('staff:list.inactive')}
                    </Tag>
                ),
            },
            {
                title: t('staff:list.columnLastActivity'),
                dataIndex: 'lastLoginAt',
                key: 'lastLoginAt',
                render: (value: string | null | undefined) =>
                    value ? formatDateTime(value, formatLocale) : '—',
            },
            {
                title: t('staff:list.columnActions'),
                key: 'actions',
                render: (_: unknown, record) => (
                    <Button
                        size="small"
                        type="link"
                        icon={<EyeOutlined />}
                        onClick={() => setDetailUser(record)}
                    >
                        {t('staff:list.view')}
                    </Button>
                ),
            },
        ],
        [formatLocale, roleDisplayLabel, t],
    );

    if (!staffPolicy.canView) {
        return (
            <Alert
                type="warning"
                showIcon
                title={t('staff:list.accessDeniedTitle')}
                description={t('staff:list.accessDeniedDescription')}
            />
        );
    }

    return (
        <>
            {!canManageUsers ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={t('staff:list.readOnlyTitle')}
                    description={t('staff:list.readOnlyDescription')}
                />
            ) : null}

            <Card
                title={t('staff:list.cardTitle')}
                extra={
                    <Space wrap>
                        <Input.Search
                            allowClear
                            placeholder={t('staff:list.search')}
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            onSearch={(value) => setSearch(value)}
                            style={{ width: 220 }}
                        />
                        <Select
                            allowClear
                            placeholder={t('staff:list.roleFilter')}
                            style={{ minWidth: 160 }}
                            value={filters.role}
                            onChange={setRoleFilter}
                            options={roles.map((role) => ({
                                value: role,
                                label: roleDisplayLabel(role),
                            }))}
                        />
                        <Select
                            allowClear
                            placeholder={t('staff:list.statusFilter')}
                            style={{ minWidth: 140 }}
                            value={filters.isActive}
                            onChange={setStatusFilter}
                            options={[
                                { value: true, label: t('staff:list.active') },
                                { value: false, label: t('staff:list.inactive') },
                            ]}
                        />
                        <Button icon={<ReloadOutlined />} onClick={() => void refetch()} loading={isFetching}>
                            {t('staff:list.refresh')}
                        </Button>
                        {canManageUsers ? (
                            <Link href="/admin/users">
                                <Button type="primary">{t('staff:list.manageUsers')}</Button>
                            </Link>
                        ) : null}
                    </Space>
                }
            >
                <Table<UserInfo>
                    rowKey={(row) => row.id ?? ''}
                    columns={columns}
                    dataSource={staff}
                    loading={isLoading && !isPlaceholderData}
                    pagination={{
                        current: page,
                        pageSize,
                        total: pagination?.totalCount ?? 0,
                        showSizeChanger: true,
                        onChange: (nextPage, nextSize) => {
                            setPage(nextPage);
                            if (nextSize !== pageSize) {
                                setPageSize(nextSize);
                                setPage(1);
                            }
                        },
                    }}
                    locale={{ emptyText: t('staff:list.empty') }}
                    style={{ opacity: isPlaceholderData ? 0.6 : 1 }}
                />
            </Card>

            <UserDetailDrawer
                open={!!detailUser}
                onClose={() => setDetailUser(null)}
                user={detailUser}
                canEditUsername={false}
                context="staff"
            />
        </>
    );
}
