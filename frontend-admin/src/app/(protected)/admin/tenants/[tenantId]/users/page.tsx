'use client';

/**
 * Super-admin: tenant user management — invite, assign existing users, owner flag.
 */
import React, { useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import {
    Alert,
    Button,
    Card,
    Modal,
    Space,
    Typography,
    message,
} from 'antd';
import {
    ArrowLeftOutlined,
    MailOutlined,
    PlusOutlined,
    ReloadOutlined,
    UserAddOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import {
    addTenantUser,
    inviteTenantUser,
    listTenantUsers,
    removeTenantUser,
    updateTenantUser,
    type TenantUserInviteResult,
} from '@/features/super-admin/api/tenantUsers';
import { getApiAdminUsers } from '@/api/generated/admin/admin';
import { AddExistingUserModal } from '@/features/super-admin/components/AddExistingUserModal';
import type { AddExistingUserFormValues } from '@/features/super-admin/components/AddExistingUserModal';
import { InviteUserModal } from '@/features/super-admin/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/super-admin/components/InviteUserModal';
import { TenantUserTable } from '@/features/super-admin/components/TenantUserTable';

const TENANT_USERS_QUERY_KEY = ['admin', 'tenant-users'] as const;

export default function SuperAdminTenantUsersPage() {
    const { t } = useI18n();
    const params = useParams();
    const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';
    const queryClient = useQueryClient();
    const { user } = useAuth();
    const [addOpen, setAddOpen] = useState(false);
    const [inviteOpen, setInviteOpen] = useState(false);
    const [inviteResult, setInviteResult] = useState<TenantUserInviteResult | null>(null);

    const canAccess =
        isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);

    const tenantQuery = useQuery({
        queryKey: ['admin', 'tenants', tenantId],
        queryFn: () => getAdminTenantById(tenantId),
        enabled: canAccess && !!tenantId,
    });

    const usersQuery = useQuery({
        queryKey: [...TENANT_USERS_QUERY_KEY, tenantId],
        queryFn: () => listTenantUsers(tenantId),
        enabled: canAccess && !!tenantId,
    });

    const allUsersQuery = useQuery({
        queryKey: ['admin', 'users', 'picker'],
        queryFn: () => getApiAdminUsers({ isActive: true }),
        enabled: canAccess && addOpen,
    });

    const invalidate = () => {
        void queryClient.invalidateQueries({ queryKey: [...TENANT_USERS_QUERY_KEY, tenantId] });
        void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants', false] });
    };

    const addMutation = useMutation({
        mutationFn: (values: AddExistingUserFormValues) =>
            addTenantUser(tenantId, {
                userId: values.userId,
                role: values.role,
                isOwner: values.isOwner,
            }),
        onSuccess: () => {
            message.success(t('tenants.users.messages.added'));
            setAddOpen(false);
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.addFailed')),
    });

    const inviteMutation = useMutation({
        mutationFn: (values: InviteUserFormValues) =>
            inviteTenantUser(tenantId, {
                email: values.email.trim(),
                role: values.role,
                isOwner: values.isOwner,
            }),
        onSuccess: (res) => {
            setInviteOpen(false);
            setInviteResult(res);
            invalidate();
            if (res.invitationEmailSent) {
                message.success(t('tenants.users.invite.messages.sent'));
            } else {
                message.warning(t('tenants.users.invite.messages.assignedNoEmail'));
            }
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
    });

    const removeMutation = useMutation({
        mutationFn: (userId: string) => removeTenantUser(tenantId, userId),
        onSuccess: () => {
            message.success(t('tenants.users.messages.removed'));
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.removeFailed')),
    });

    const setOwnerMutation = useMutation({
        mutationFn: (userId: string) => updateTenantUser(tenantId, userId, { isOwner: true }),
        onSuccess: () => {
            message.success(t('tenants.users.messages.ownerSet'));
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.ownerSetFailed')),
    });

    const assignedIds = useMemo(
        () => new Set((usersQuery.data ?? []).map((u) => u.userId)),
        [usersQuery.data],
    );

    const userPickerOptions = useMemo(() => {
        const list = Array.isArray(allUsersQuery.data) ? allUsersQuery.data : [];
        return list
            .filter((u) => u.id && !assignedIds.has(u.id) && u.role !== 'SuperAdmin')
            .map((u) => {
                const label = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.userName || u.id;
                return {
                    value: u.id!,
                    label: `${label} (${u.email ?? u.userName ?? u.id})`,
                };
            });
    }, [allUsersQuery.data, assignedIds]);

    if (!canAccess) {
        return (
            <AdminPageShell>
                <Alert type="error" message={t('tenants.accessDenied.title')} description={t('tenants.accessDenied.body')} />
            </AdminPageShell>
        );
    }

    if (!tenantId) {
        return (
            <AdminPageShell>
                <Alert type="error" message={t('tenants.users.errors.invalidTenant')} />
            </AdminPageShell>
        );
    }

    const tenant = tenantQuery.data;
    const tenantLabel = tenant ? `${tenant.name} (${tenant.slug})` : tenantId;

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('tenants.users.page.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
                    { title: t('tenants.page.title'), href: '/admin/tenants' },
                    { title: tenantLabel, href: `/admin/tenants/${tenantId}/users` },
                ]}
                actions={
                    <Space wrap>
                        <Link href="/admin/tenants">
                            <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
                        </Link>
                        <Button icon={<ReloadOutlined />} onClick={() => invalidate()}>
                            {t('common.refresh')}
                        </Button>
                        <Button icon={<UserAddOutlined />} onClick={() => setAddOpen(true)}>
                            {t('tenants.users.actions.add')}
                        </Button>
                        <Button type="primary" icon={<MailOutlined />} onClick={() => setInviteOpen(true)}>
                            {t('tenants.users.invite.action')}
                        </Button>
                    </Space>
                }
            />

            <Typography.Paragraph type="secondary">{t('tenants.users.page.subtitle')}</Typography.Paragraph>

            {tenantQuery.isError ? (
                <Alert type="error" message={t('tenants.users.errors.tenantNotFound')} style={{ marginBottom: 16 }} />
            ) : null}

            <Card>
                <TenantUserTable
                    users={usersQuery.data ?? []}
                    loading={usersQuery.isLoading || tenantQuery.isLoading}
                    setOwnerPending={setOwnerMutation.isPending}
                    removePending={removeMutation.isPending}
                    onSetOwner={(userId) => setOwnerMutation.mutate(userId)}
                    onRemove={(userId) => removeMutation.mutate(userId)}
                />
            </Card>

            <AddExistingUserModal
                open={addOpen}
                confirmLoading={addMutation.isPending}
                loadingUsers={allUsersQuery.isLoading}
                userOptions={userPickerOptions}
                onClose={() => setAddOpen(false)}
                onSubmit={(values) => addMutation.mutate(values)}
            />

            <InviteUserModal
                open={inviteOpen}
                confirmLoading={inviteMutation.isPending}
                onClose={() => setInviteOpen(false)}
                onSubmit={(values) => inviteMutation.mutate(values)}
            />

            <Modal
                title={t('tenants.users.invite.resultTitle')}
                open={!!inviteResult}
                onCancel={() => setInviteResult(null)}
                footer={[
                    <Button key="ok" type="primary" onClick={() => setInviteResult(null)}>
                        {t('common.ok', { defaultValue: 'OK' })}
                    </Button>,
                ]}
            >
                {inviteResult ? (
                    <Space direction="vertical" style={{ width: '100%' }}>
                        <Typography.Text>
                            {inviteResult.userCreated
                                ? t('tenants.users.invite.resultCreated')
                                : t('tenants.users.invite.resultExisting')}
                        </Typography.Text>
                        <Typography.Text type="secondary">{inviteResult.emailDeliveryNote}</Typography.Text>
                        {inviteResult.generatedPassword ? (
                            <Alert
                                type="warning"
                                showIcon
                                message={t('tenants.users.invite.passwordOnce')}
                                description={
                                    <Typography.Text code copyable>
                                        {inviteResult.generatedPassword}
                                    </Typography.Text>
                                }
                            />
                        ) : null}
                    </Space>
                ) : null}
            </Modal>
        </AdminPageShell>
    );
}
