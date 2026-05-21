'use client';

import { useMemo, useState } from 'react';
import { Alert, Button, Modal, Space, Typography, message } from 'antd';
import { MailOutlined, ReloadOutlined, UserAddOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { getApiAdminUsers } from '@/api/generated/admin/admin';
import { AddExistingUserModal } from '@/features/super-admin/components/AddExistingUserModal';
import type { AddExistingUserFormValues } from '@/features/super-admin/components/AddExistingUserModal';
import { InviteUserModal } from '@/features/super-admin/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/super-admin/components/InviteUserModal';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import { TenantUserTable } from '@/features/super-admin/components/TenantUserTable';
import {
    addTenantUser,
    inviteTenantUser,
    listTenantUsers,
    removeTenantUser,
    updateTenantUser,
    updateTenantUserRole,
    type TenantUser,
    type TenantUserInviteResult,
} from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';

const TENANT_USERS_QUERY_KEY = ['admin', 'tenant-users'] as const;

export type TenantDetailUsersTabProps = {
    tenantId: string;
};

export function TenantDetailUsersTab({ tenantId }: TenantDetailUsersTabProps) {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [addOpen, setAddOpen] = useState(false);
    const [inviteOpen, setInviteOpen] = useState(false);
    const [inviteResult, setInviteResult] = useState<TenantUserInviteResult | null>(null);
    const [resetUser, setResetUser] = useState<TenantUser | null>(null);
    const [smtpHint, setSmtpHint] = useState(false);
    const [roleChangeUserId, setRoleChangeUserId] = useState<string | null>(null);

    const usersQuery = useQuery({
        queryKey: [...TENANT_USERS_QUERY_KEY, tenantId],
        queryFn: () => listTenantUsers(tenantId),
    });

    const allUsersQuery = useQuery({
        queryKey: ['admin', 'users', 'picker'],
        queryFn: () => getApiAdminUsers({ isActive: true }),
        enabled: addOpen,
    });

    const invalidate = () => {
        void queryClient.invalidateQueries({ queryKey: [...TENANT_USERS_QUERY_KEY, tenantId] });
        void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
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
            setSmtpHint(res.invitationEmailSent);
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

    const roleMutation = useMutation({
        mutationFn: ({ userId, role }: { userId: string; role: string }) =>
            updateTenantUserRole(tenantId, userId, role),
        onMutate: ({ userId }) => setRoleChangeUserId(userId),
        onSettled: () => setRoleChangeUserId(null),
        onSuccess: () => {
            message.success(t('tenants.users.messages.roleUpdated'));
            invalidate();
        },
        onError: () => message.error(t('tenants.users.messages.roleUpdateFailed')),
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

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Space wrap>
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

            <TenantUserTable
                users={usersQuery.data ?? []}
                loading={usersQuery.isLoading}
                setOwnerPending={setOwnerMutation.isPending}
                removePending={removeMutation.isPending}
                roleChangeUserId={roleChangeUserId}
                onSetOwner={(userId) => setOwnerMutation.mutate(userId)}
                onRemove={(userId) => removeMutation.mutate(userId)}
                onRoleChange={(userId, role) => roleMutation.mutate({ userId, role })}
                onResetPassword={(userId) => {
                    const row = usersQuery.data?.find((u) => u.userId === userId) ?? null;
                    setResetUser(row);
                }}
            />

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
                fixedTenantId={tenantId}
                showOwnerToggle
                confirmLoading={inviteMutation.isPending}
                onClose={() => setInviteOpen(false)}
                onSubmit={(values) => inviteMutation.mutate(values)}
            />

            <ResetPasswordModal
                open={!!resetUser}
                tenantId={tenantId}
                user={resetUser}
                smtpConfigured={smtpHint}
                onClose={() => setResetUser(null)}
                onCompleted={invalidate}
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
        </Space>
    );
}
