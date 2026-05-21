'use client';

import { useMemo, useState } from 'react';
import { Button, Space, message } from 'antd';
import { ReloadOutlined, ThunderboltOutlined, UserAddOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { listPlatformUsers } from '@/features/users/api/users';
import { AddExistingUserModal } from '@/features/super-admin/components/AddExistingUserModal';
import type { AddExistingUserFormValues } from '@/features/super-admin/components/AddExistingUserModal';
import { InviteUserModal } from '@/features/super-admin/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/super-admin/components/InviteUserModal';
import { createQuickUser, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { QuickUserModal } from '@/features/super-admin/components/QuickUserModal';
import type { QuickUserFormValues } from '@/features/super-admin/components/QuickUserModal';
import { ResetPasswordModal } from '@/features/super-admin/components/ResetPasswordModal';
import { TenantUserTable } from '@/features/super-admin/components/TenantUserTable';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
    assignTenantUser,
    createTenantUser,
    listTenantUsers,
    removeTenantUser,
    updateTenantUser,
    updateTenantUserRole,
    type CreateTenantUserResult,
    type TenantUser,
} from '@/features/super-admin/api/tenantUsers';
import { SuperAdminCredentialsGate } from '@/features/super-admin/components/SuperAdminCredentialsGate';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { UserCreatedSuccessModal } from '@/features/super-admin/components/UserCreatedSuccessModal';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { InviteTenantContextBanner } from '@/features/users/components/InviteTenantContextBanner';
import { useI18n } from '@/i18n';

const TENANT_USERS_QUERY_KEY = ['admin', 'tenant-users'] as const;

export type TenantDetailUsersTabProps = {
    tenantId: string;
    tenant?: AdminTenantListItem | null;
};

export function TenantDetailUsersTab({ tenantId, tenant }: TenantDetailUsersTabProps) {
    const { t } = useI18n();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();
    const queryClient = useQueryClient();
    const [addOpen, setAddOpen] = useState(false);
    const [inviteOpen, setInviteOpen] = useState(false);
    const [quickOpen, setQuickOpen] = useState(false);
    const [createResult, setCreateResult] = useState<CreateTenantUserResult | null>(null);
    const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
    const [quickRole, setQuickRole] = useState<string>('Manager');
    const [resetUser, setResetUser] = useState<TenantUser | null>(null);
    const [roleChangeUserId, setRoleChangeUserId] = useState<string | null>(null);

    const usersQuery = useQuery({
        queryKey: [...TENANT_USERS_QUERY_KEY, tenantId],
        queryFn: () => listTenantUsers(tenantId),
    });

    const allUsersQuery = useQuery({
        queryKey: ['admin', 'users', 'picker', 'platform'],
        queryFn: () => listPlatformUsers({ isActive: true }),
        enabled: addOpen,
    });

    const invalidate = () => {
        void queryClient.invalidateQueries({ queryKey: [...TENANT_USERS_QUERY_KEY, tenantId] });
        void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
    };

    const addMutation = useMutation({
        mutationFn: (values: AddExistingUserFormValues) =>
            assignTenantUser(tenantId, {
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

    const createMutation = useMutation({
        mutationFn: (values: InviteUserFormValues) =>
            createTenantUser(tenantId, {
                email: values.email.trim(),
                role: values.role,
                isOwner: values.isOwner,
            }),
        onSuccess: (res) => {
            setInviteOpen(false);
            setCreateResult(res);
            invalidate();
            message.success(t('tenants.users.invite.messages.created'));
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
    });

    const quickMutation = useMutation({
        mutationFn: (values: QuickUserFormValues) => createQuickUser(tenantId, { role: values.role }),
        onSuccess: (res, values) => {
            setQuickOpen(false);
            setQuickRole(values.role);
            setQuickResult(res);
            invalidate();
            message.success(t('tenants.users.quick.messages.created'));
        },
        onError: (err: unknown) => {
            const status = (err as { response?: { status?: number } })?.response?.status;
            if (status === 429) {
                message.error(t('tenants.users.quick.messages.rateLimited'));
                return;
            }
            message.error(t('tenants.users.quick.messages.failed'));
        },
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
            {tenant ? <InviteTenantContextBanner tenant={tenant} variant="page" /> : null}
            <Space wrap>
                <Button icon={<ReloadOutlined />} onClick={() => invalidate()}>
                    {t('common.refresh')}
                </Button>
                <Button icon={<UserAddOutlined />} onClick={() => setAddOpen(true)}>
                    {t('tenants.users.actions.add')}
                </Button>
                <SuperAdminCredentialsGate showRestrictedHint={false}>
                    <Button type="primary" icon={<UserAddOutlined />} onClick={() => setInviteOpen(true)}>
                        {t('tenants.users.invite.action')}
                    </Button>
                    <Button icon={<ThunderboltOutlined />} onClick={() => setQuickOpen(true)}>
                        {t('tenants.users.quick.action')}
                    </Button>
                </SuperAdminCredentialsGate>
            </Space>

            {!canProvisionTenantCredentials ? (
                <SuperAdminCredentialsGate />
            ) : null}

            <TenantUserTable
                users={usersQuery.data ?? []}
                loading={usersQuery.isLoading}
                setOwnerPending={setOwnerMutation.isPending}
                removePending={removeMutation.isPending}
                roleChangeUserId={roleChangeUserId}
                onSetOwner={(userId) => setOwnerMutation.mutate(userId)}
                onRemove={(userId) => removeMutation.mutate(userId)}
                onRoleChange={(userId, role) => roleMutation.mutate({ userId, role })}
                onResetPassword={
                    canProvisionTenantCredentials
                        ? (userId) => {
                              const row = usersQuery.data?.find((u) => u.userId === userId) ?? null;
                              setResetUser(row);
                          }
                        : undefined
                }
            />

            <AddExistingUserModal
                open={addOpen}
                confirmLoading={addMutation.isPending}
                loadingUsers={allUsersQuery.isLoading}
                userOptions={userPickerOptions}
                onClose={() => setAddOpen(false)}
                onSubmit={(values) => addMutation.mutate(values)}
            />

            {canProvisionTenantCredentials ? (
                <>
                    <InviteUserModal
                        open={inviteOpen}
                        variant="tenantDetail"
                        tenantId={tenantId}
                        tenantContext={tenant ?? undefined}
                        showOwnerToggle
                        confirmLoading={createMutation.isPending}
                        onClose={() => setInviteOpen(false)}
                        onSubmit={(values) => createMutation.mutate(values)}
                    />

                    <QuickUserModal
                        open={quickOpen}
                        tenantSlug={tenant?.slug ?? 'tenant'}
                        tenantName={tenant?.name}
                        confirmLoading={quickMutation.isPending}
                        onClose={() => setQuickOpen(false)}
                        onSubmit={(values) => quickMutation.mutate(values)}
                    />

                    <ResetPasswordModal
                        open={!!resetUser}
                        tenantId={tenantId}
                        user={resetUser}
                        onClose={() => setResetUser(null)}
                        onCompleted={invalidate}
                    />

                    <UserCreatedSuccessModal
                        open={!!createResult}
                        result={createResult}
                        onClose={() => setCreateResult(null)}
                    />

                    <QuickUserSuccessModal
                        open={!!quickResult}
                        result={quickResult}
                        role={quickRole}
                        tenantName={tenant?.name ?? tenantId}
                        tenantSlug={tenant?.slug ?? 'tenant'}
                        onClose={() => setQuickResult(null)}
                        onGenerateAnother={() => {
                            setQuickResult(null);
                            setQuickOpen(true);
                        }}
                    />
                </>
            ) : null}
        </Space>
    );
}
