'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import {
    Alert,
    Button,
    Card,
    Descriptions,
    Space,
    Spin,
    Tag,
    Typography,
} from 'antd';
import { ArrowLeftOutlined, EditOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import {
    adminUsersQueryKeys,
    getAdminUserById,
} from '@/features/users/api/users';
import { EditUsernameModal } from '@/features/users/components/EditUsernameModal';
import { UserActivityTimeline } from '@/features/users/components/UserActivityTimeline';
import { UserTenantSummary } from '@/features/users/components/UserTenantSummary';
import { useAdminUserTenants } from '@/features/users/hooks/useAdminUserTenants';

function displayName(firstName?: string | null, lastName?: string | null, userName?: string | null): string {
    const name = `${firstName ?? ''} ${lastName ?? ''}`.trim();
    return name || userName?.trim() || '—';
}

export function AdminUserDetailPage() {
    const { t, formatLocale } = useI18n();
    const params = useParams();
    const queryClient = useQueryClient();
    const { user: currentUser } = useAuth();
    const policy = useUsersPolicy();
    const userId = typeof params.id === 'string' ? params.id : '';

    const [usernameModalOpen, setUsernameModalOpen] = useState(false);

    const canAccess = policy.canView && isSuperAdmin(currentUser?.role);

    const userQuery = useQuery({
        queryKey: adminUsersQueryKeys.detail(userId),
        queryFn: () => getAdminUserById(userId),
        enabled: canAccess && !!userId,
    });

    const { data: memberships = [], isLoading: tenantsLoading } = useAdminUserTenants(
        userId,
        canAccess && !!userId,
    );

    const user = userQuery.data;
    const title = user
        ? displayName(user.firstName, user.lastName, user.userName)
        : t('users.page.title');

    const invalidateLists = () => {
        void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
        void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.detail(userId) });
    };

    if (!canAccess) {
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
                title={title}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('users.page.title'), href: '/admin/users' },
                    { title },
                ]}
                actions={
                    <Link href="/admin/users">
                        <Button icon={<ArrowLeftOutlined />}>{t('users.detail.backToList')}</Button>
                    </Link>
                }
            />

            {userQuery.isLoading ? (
                <Spin />
            ) : null}

            {userQuery.isError ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('users.list.errorLoad')}
                    action={
                        <Button size="small" onClick={() => void userQuery.refetch()}>
                            {t('users.list.retry')}
                        </Button>
                    }
                />
            ) : null}

            {user ? (
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <Card title={t('users.list.details')}>
                        <Descriptions column={1} bordered size="small">
                            <Descriptions.Item label={t('users.list.columnStatus')}>
                                <Tag color={user.isActive ? 'green' : 'red'}>
                                    {user.isActive ? t('users.list.statusActive') : t('users.list.statusInactive')}
                                </Tag>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('users.list.columnRole')}>
                                <Tag color="gold">{user.role ?? '—'}</Tag>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('users.tabs.tenant.columnTenant')}>
                                <UserTenantSummary
                                    userRole={user.role ?? undefined}
                                    memberships={memberships}
                                    loading={tenantsLoading}
                                />
                            </Descriptions.Item>
                            <Descriptions.Item label={t('users.form.userName')}>
                                <Space wrap>
                                    <Typography.Text>{user.userName?.trim() || '—'}</Typography.Text>
                                    {policy.canEdit && userId ? (
                                        <Button
                                            type="link"
                                            size="small"
                                            icon={<EditOutlined />}
                                            onClick={() => setUsernameModalOpen(true)}
                                        >
                                            {t('users.username.editTitle')}
                                        </Button>
                                    ) : null}
                                </Space>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('users.list.columnEmail')}>
                                {user.email ?? '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('users.form.employeeNumber')}>
                                {user.employeeNumber ?? '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('users.list.columnLastLogin')}>
                                {user.lastLoginAt
                                    ? formatDateTime(user.lastLoginAt, formatLocale)
                                    : '—'}
                            </Descriptions.Item>
                        </Descriptions>
                    </Card>

                    <Card
                        title={t('users.activity.tabLabel')}
                        extra={
                            userId ? (
                                <Link href={`/admin/reports/user-activity?userId=${encodeURIComponent(userId)}`}>
                                    <Button type="link" size="small">
                                        {t('users.activity.openFullReport')}
                                    </Button>
                                </Link>
                            ) : null
                        }
                    >
                        <UserActivityTimeline
                            userId={userId}
                            userName={displayName(user.firstName, user.lastName, user.userName)}
                        />
                    </Card>
                </Space>
            ) : null}

            {userId ? (
                <EditUsernameModal
                    open={usernameModalOpen}
                    userId={userId}
                    currentUsername={user?.userName ?? ''}
                    userEmail={user?.email}
                    onClose={() => setUsernameModalOpen(false)}
                    onSuccess={() => {
                        invalidateLists();
                        setUsernameModalOpen(false);
                    }}
                />
            ) : null}
        </AdminPageShell>
    );
}
