'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * User management – RKSV/DSGVO-aligned user lifecycle.
 * Table: name, email, role, branch, status, last login, actions.
 * Filters: role, status, branch, search. Drawer create/edit, deactivate (reason), reactivate, activity timeline tab.
 */
import React, { useState, useMemo, useEffect, useCallback, useRef } from 'react';
import { Modal, Card, Typography, Tag, Space, Button, Alert, Flex, Tooltip, Descriptions } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import {
    UserOutlined,
} from '@ant-design/icons';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { AccessSecondaryNav } from '@/features/access/components/AccessSecondaryNav';
import { useQueryClient, useMutation, useQuery } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { useRoles } from '@/features/users/hooks/useRoles';
import { useI18n } from '@/i18n/I18nProvider';
import { useCreateRoleMutation } from '@/features/users/hooks/useCreateRoleMutation';
import {
    listQueryKey,
    rolesQueryKey,
    createUser as gatewayCreateUser,
    updateUser as gatewayUpdateUser,
    getUserById,
    getUserByIdQueryKey,
    deactivateUser as gatewayDeactivateUser,
    reactivateUser as gatewayReactivateUser,
    resetPassword as gatewayResetPassword,
    normalizeError,
    type UserInfo,
    type CreateUserRequest,
    type UpdateUserRequest,
} from '@/features/users/api/usersGateway';
import { UserDetailDrawer } from '@/features/users/components/UserDetailDrawer';
import { EditUsernameModal } from '@/features/users/components/EditUsernameModal';
import { UserFormDrawer, type UserFormSubmitValues } from '@/features/users/components/UserFormDrawer';
import { UserPermissionsModal } from '@/features/users/components/UserPermissionsModal';
import {
    CreateRoleModal,
    DeactivateUserModal,
    ResetPasswordUserModal,
} from '@/features/users/components/UsersPageActionModals';
import { ResetPasswordModal } from '@/features/users/components/ResetPasswordModal';
import { createUsersFormRules, buildUsersFormRulesContext, mapBackendPasswordError } from '@/features/users/constants/validation';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { getTenantSwitcherLicenseBadge } from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { UnifiedAdminUsersView } from '@/features/users/components/UnifiedAdminUsersView';
import { DevOrphanedUsersCleanupButton } from '@/features/users/components/DevOrphanedUsersCleanupButton';
import { createPlatformUser, getAdminUserTenants, updateUserTenants } from '@/features/users/api/users';
import { useUpdateUserRole } from '@/features/users/hooks/useUpdateUserRole';
import { resolveRoleChangeTenantId } from '@/features/users/utils/resolveRoleChangeTenantId';
import { shouldUseTenantRoleChangeApi } from '@/features/users/utils/roleChangeTenantApi';

function fullName(record: UserInfo): string {
    const first = record.firstName ?? '';
    const last = record.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || record.userName || record.id || '—';
}

import { formatRoleDisplayLabel, CANONICAL_ROLE_NAMES } from '@/features/users/utils/roleDisplayLabel';

/** Aktiver Mandant-Kontext (Badge + API) — einheitlich über useCurrentTenant. */
function UsersPageActiveTenantContext() {
    const { t } = useI18n();
    const {
        hasAuthToken,
        isSuperAdminPlatformMode,
        isRealTenantSlug,
        tenantName,
        tenantSlug,
        tenantId,
        isTenantSuspended,
        licenseValidUntilUtc,
        licenseKey,
        isTenantRecordLoading,
        isDevTenantOverride,
        isImpersonating,
    } = useCurrentTenant();

    const licenseBadge = useMemo(
        () =>
            getTenantSwitcherLicenseBadge(
                { licenseValidUntilUtc: licenseValidUntilUtc ?? null, licenseKey: licenseKey ?? null },
                t,
            ),
        [licenseValidUntilUtc, licenseKey, t],
    );

    if (!hasAuthToken || isSuperAdminPlatformMode || !isRealTenantSlug) {
        return null;
    }

    const displayName = tenantName?.trim() || tenantSlug || '—';

    return (
        <Card
            size="small"
            loading={isTenantRecordLoading}
            style={{ marginBottom: 16 }}
            title={t('common.tenant.badgeDualLabel', { name: displayName })}
        >
            {isTenantSuspended ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 12 }}
                    title={t('adminShell.tenant.devSwitcher.suspendedSuffix')}
                />
            ) : null}
            <Flex gap={4} wrap="wrap" style={{ marginBottom: 8 }}>
                {isImpersonating ? (
                    <Tag color="purple">{t('adminShell.tenant.infoCardImpersonationTag')}</Tag>
                ) : null}
                {isDevTenantOverride ? (
                    <Tag color="gold">{t('adminShell.tenant.infoCardDevOverrideTag')}</Tag>
                ) : null}
                {licenseBadge ? (
                    <Tooltip title={licenseBadge.tooltip}>
                        <Tag color={licenseBadge.color}>{licenseBadge.label}</Tag>
                    </Tooltip>
                ) : null}
            </Flex>
            <Descriptions column={1} size="small">
                <Descriptions.Item label={t('adminShell.tenant.infoCardName')}>
                    {tenantName || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.slug')}>
                    <Typography.Text code copyable>
                        {tenantSlug}
                    </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.infoCardId')}>
                    {tenantId ? (
                        <Typography.Text code copyable={{ text: tenantId }}>
                            {tenantId}
                        </Typography.Text>
                    ) : (
                        '—'
                    )}
                </Descriptions.Item>
            </Descriptions>
        </Card>
    );
}

export default function UsersPage() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const pathname = usePathname();
    const router = useRouter();
    const searchParams = useSearchParams();

    const roleDisplayLabel = useCallback(
        (roleName: string) => formatRoleDisplayLabel(t, roleName),
        [t],
    );
    const [createOpen, setCreateOpen] = useState(false);
    /** Edit: only store selected user id. Detail is fetched by useQuery below; never use list row data for edit form. */
    const [editUserId, setEditUserId] = useState<string | null>(null);
    const [detailUser, setDetailUser] = useState<UserInfo | null>(null);
    const [deactivateUserRecord, setDeactivateUserRecord] = useState<UserInfo | null>(null);
    const [reactivateUserRecord, setReactivateUserRecord] = useState<UserInfo | null>(null);
    const [resetPasswordUser, setResetPasswordUser] = useState<UserInfo | null>(null);
    const [usernameEditUser, setUsernameEditUser] = useState<UserInfo | null>(null);
    const [permissionsUser, setPermissionsUser] = useState<UserInfo | null>(null);
    const [createRoleOpen, setCreateRoleOpen] = useState(false);
    const [resetPasswordValidationError, setResetPasswordValidationError] = useState<string | null>(null);
    const { user: currentUser } = useAuth();
    const { tenantId: currentTenantId, isRealTenantSlug } = useCurrentTenant();
    const policy = useUsersPolicy();
    const isSuperAdminLayout = isSuperAdmin(currentUser?.role);

    useEffect(() => {
        if (isSuperAdminLayout && pathname === '/users') {
            const params = new URLSearchParams(
                typeof window !== 'undefined' ? window.location.search : '',
            );
            params.delete('create');
            params.delete('platform');
            const search = params.toString();
            router.replace(`/admin/users${search ? `?${search}` : ''}`);
        }
    }, [isSuperAdminLayout, pathname, router]);

    /** Strip command-palette query params; user detail deep link only — never auto-open create modal. */
    const paletteDeepLinkHandledRef = useRef(false);
    useEffect(() => {
        if (!policy.canView) return;
        const create = searchParams.get('create');
        const userId = searchParams.get('userId')?.trim();
        const hasPaletteParams = create === '1' || Boolean(userId);
        if (!hasPaletteParams) {
            paletteDeepLinkHandledRef.current = false;
            return;
        }
        if (paletteDeepLinkHandledRef.current) return;
        paletteDeepLinkHandledRef.current = true;

        const basePath = pathname?.startsWith('/admin/users') ? '/admin/users' : '/users';
        router.replace(basePath, { scroll: false });

        if (userId) {
            void getUserById(userId)
                .then((u) => setDetailUser(u))
                .catch(() => message.error(t('users.messages.errorLoadUser')));
        }
    }, [searchParams, policy.canView, pathname, router, message]);

    const queryClient = useQueryClient();

    const { data: roles, isLoading: rolesLoading } = useRoles({ enabled: policy.canView || !!editUserId });
    const canManageRoles = policy.canCreateRole || policy.canDeleteRole || policy.canEditRolePermissions;
    // Edit flow: when editUserId is set, fetch full user detail (GET /api/UserManagement/{id}); form is filled from this response only.
    const { data: editUserFull, isLoading: editUserLoading, isError: editUserError, error: editUserFetchError, refetch: refetchEditUser } = useQuery({
        queryKey: getUserByIdQueryKey(editUserId ?? ''),
        queryFn: () => getUserById(editUserId!),
        enabled: !!editUserId,
    });
    const modalRules = useMemo(() => createUsersFormRules(buildUsersFormRulesContext(t)), [t]);

    // Role list for form + filter: always from full catalog (useRoles). Never from selected user or assigned subset.
    const roleOptions = useMemo(
        () =>
            roles?.map((r) => ({ value: r, label: roleDisplayLabel(r) })) ??
            CANONICAL_ROLE_NAMES.map((value) => ({
                value,
                label: roleDisplayLabel(value),
            })),
        [roles, t, roleDisplayLabel],
    );

    const [createPlatformMode, setCreatePlatformMode] = useState(false);

    useEffect(() => {
        setCreateOpen(false);
        setCreatePlatformMode(false);
        return () => {
            setCreateOpen(false);
            setCreatePlatformMode(false);
        };
    }, [pathname]);

    const invalidateAllUserLists = () => {
        void queryClient.invalidateQueries({ queryKey: listQueryKey });
        void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    };

    const createMutation = useMutation({
        mutationFn: (payload: { platform: boolean; data: CreateUserRequest }) =>
            payload.platform
                ? createPlatformUser({
                      email: payload.data.email ?? '',
                      firstName: payload.data.firstName,
                      lastName: payload.data.lastName,
                      role: 'SuperAdmin',
                  }).then(() => undefined)
                : gatewayCreateUser(payload.data),
        onSuccess: (_data, { platform }) => {
            message.success(t('users.messages.successCreate'));
            if (platform) {
                invalidateAllUserLists();
            } else {
                invalidateAllUserLists();
            }
            setCreateOpen(false);
            setCreatePlatformMode(false);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, t('users.messages.errorGeneric')).message);
        },
    });
    const updateUserRoleMutation = useUpdateUserRole();
    const updateMutation = useMutation({
        mutationFn: ({ id, data }: { id: string; data: UpdateUserRequest }) => gatewayUpdateUser(id, data),
    });
    const updateUserTenantsMutation = useMutation({
        mutationFn: ({ id, tenantIds }: { id: string; tenantIds: string[] }) => updateUserTenants(id, tenantIds),
    });
    const resetPasswordMutation = useMutation({
        mutationFn: ({ id, data }: { id: string; data: { newPassword: string } }) => gatewayResetPassword(id, data),
        onSuccess: (_data, { id }) => {
            message.success(t('users.messages.successResetPassword'));
            invalidateAllUserLists();
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setResetPasswordUser(null);
            setResetPasswordValidationError(null);
        },
        onError: (e: unknown) => {
            const err = e as { response?: { status?: number; data?: { errors?: Record<string, string[]> } } };
            const status = err.response?.status;
            const is401 = status === 401;
            const is403 = status === 403;
            const is404 = status === 404;
            const is400 = status === 400;
            const fallback =
                is401 ? t('users.messages.sessionExpiredOrUnauthorized')
                : is403 ? t('users.messages.errorResetPasswordForbidden')
                : is404 ? t('users.messages.errorResetPasswordUserNotFound')
                : t('users.messages.errorResetPassword');
            const normalized = normalizeError(e, fallback);
            const data = err.response?.data as { errors?: Record<string, string[]> } | undefined;
            const errors = data?.errors;
            const fieldErrors = errors?.newPassword ?? errors?.NewPassword;
            const firstBackendMessage = Array.isArray(fieldErrors) && fieldErrors.length > 0 ? fieldErrors[0] : normalized.message;
            const displayMessage = mapBackendPasswordError(t, firstBackendMessage);

            if (is400) {
                setResetPasswordValidationError(displayMessage);
            } else {
                message.error(normalized.message);
            }
        },
    });
    const deactivateMutation = useMutation({
        mutationFn: ({ id, reason }: { id: string; reason: string }) => gatewayDeactivateUser(id, { reason }),
        onSuccess: (_data, { id }) => {
            message.success(t('users.messages.successDeactivate'));
            invalidateAllUserLists();
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setDeactivateUserRecord(null);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, t('users.messages.errorGeneric')).message);
        },
    });
    const reactivateMutation = useMutation({
        mutationFn: (id: string) => gatewayReactivateUser(id),
        onSuccess: (_data, id) => {
            message.success(t('users.messages.successReactivate'));
            invalidateAllUserLists();
            queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${id}`] });
            setReactivateUserRecord(null);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, t('users.messages.errorGeneric')).message);
        },
    });
    const createRoleMutation = useCreateRoleMutation({
        onSuccess: () => setCreateRoleOpen(false),
    });
    const handleCreate = (values: CreateUserRequest | UpdateUserRequest) => {
        if (!policy.canCreate) {
            message.error(t('users.messages.noPermission'));
            return;
        }
        const raw = values as CreateUserRequest;
        const createPayload: CreateUserRequest = {
            ...raw,
            employeeNumber: (raw.employeeNumber ?? '').trim(),
            ...(createPlatformMode ? { role: 'SuperAdmin' } : {}),
        };
        createMutation.mutate({ platform: createPlatformMode, data: createPayload });
    };
    const submitEditUser = async (values: UserFormSubmitValues, userId: string) => {
        const { tenantIds, ...restValues } = values as UpdateUserRequest & { tenantIds?: string[] };
        const updatePayload: UpdateUserRequest = {
            ...restValues,
            employeeNumber: (restValues.employeeNumber ?? '').trim(),
        };
        const previousRole = editUserFull?.role?.trim() ?? '';
        const newRole = updatePayload.role?.trim() ?? '';

        try {
            if (shouldUseTenantRoleChangeApi(previousRole, newRole)) {
                let tenantIdForRole: string | undefined;

                if (isRealTenantSlug && currentTenantId) {
                    tenantIdForRole = currentTenantId;
                } else {
                    const memberships = await getAdminUserTenants(userId);
                    tenantIdForRole = resolveRoleChangeTenantId(
                        memberships,
                        isRealTenantSlug ? currentTenantId : null,
                    );
                }

                if (tenantIdForRole) {
                    await updateUserRoleMutation.mutateAsync({
                        tenantId: tenantIdForRole,
                        userId,
                        role: newRole,
                    });
                }
            }

            await updateMutation.mutateAsync({ id: userId, data: updatePayload });
            if (isSuperAdminLayout && Array.isArray(tenantIds)) {
                await updateUserTenantsMutation.mutateAsync({ id: userId, tenantIds });
                await queryClient.invalidateQueries({ queryKey: ['admin', 'users', userId, 'tenants'] });
                message.success(t('users.tenants.manageSaved'));
            } else {
                message.success(t('users.messages.successUpdate'));
            }
            await queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(userId) });
            invalidateAllUserLists();
            await queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${userId}`] });
            setEditUserId(null);
        } catch (e: unknown) {
            message.error(normalizeError(e, t('users.messages.errorGeneric')).message);
            throw e;
        }
    };

    const handleEdit = async (values: UserFormSubmitValues) => {
        if (!editUserId) return;
        if (!policy.canEdit) {
            message.error(t('users.messages.noPermission'));
            return;
        }
        await submitEditUser(values, editUserId);
    };

    const handleCreateRoleConfirm = (payload: { name: string; inheritFromRole?: string }) => {
        if (!policy.canCreateRole) {
            message.error(t('users.messages.noPermission'));
            return;
        }
        createRoleMutation.mutate(payload);
    };
    const handleDeactivateConfirm = (reason: string) => {
        if (!deactivateUserRecord?.id) return;
        if (!policy.canDeactivate) {
            message.error(t('users.messages.noPermission'));
            return;
        }
        deactivateMutation.mutate({ id: deactivateUserRecord.id, reason });
    };
    const handleReactivate = () => {
        if (!reactivateUserRecord?.id) return;
        if (!policy.canReactivate) {
            message.error(t('users.messages.noPermission'));
            return;
        }
        reactivateMutation.mutate(reactivateUserRecord.id);
    };
    const handleResetPasswordConfirm = (newPassword: string) => {
        if (!resetPasswordUser?.id) return;
        if (!policy.canResetPassword(resetPasswordUser.role)) {
            message.error(t('users.messages.noPermission'));
            return;
        }
        resetPasswordMutation.mutate({ id: resetPasswordUser.id, data: { newPassword } });
    };
    const handleClearResetPasswordValidationError = useCallback(() => {
        setResetPasswordValidationError(null);
    }, []);

    const handleUsernameUpdated = useCallback(
        (userId: string, result: { newUsername: string }) => {
            setDetailUser((prev) =>
                prev?.id === userId ? { ...prev, userName: result.newUsername } : prev,
            );
            invalidateAllUserLists();
            if (editUserId === userId) {
                void refetchEditUser();
            }
        },
        [editUserId, refetchEditUser, queryClient],
    );

    if (!policy.canView) {
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
            {!isSuperAdminLayout ? <AccessSecondaryNav /> : null}
            <AdminPageHeader
                title={t('users.page.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    ...(isSuperAdminLayout
                        ? [{ title: t('users.page.title') }]
                        : [
                              { title: t('access.hub.pageTitle'), href: '/admin/access' },
                              { title: t('users.page.title') },
                          ]),
                ]}
                actions={
                    <Space wrap>
                        {policy.canCreateRole && (
                            <Button icon={<UserOutlined />} onClick={() => setCreateRoleOpen(true)}>
                                {t('users.page.createRole')}
                            </Button>
                        )}
                        {canManageRoles && (
                            <Link href="/admin/access/roles">
                                <Button type="default">{t('users.page.manageRoles')}</Button>
                            </Link>
                        )}
                        {isSuperAdminLayout ? (
                            <DevOrphanedUsersCleanupButton invalidatePlatformUsers />
                        ) : null}
                    </Space>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                    {isSuperAdminLayout ? t('users.unified.pageIntro') : t('users.list.pageIntro')}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                    {t('users.list.forensicsHintLead')}{' '}
                    <Link href="/audit-logs">{t('users.list.forensicsLinkAuditLog')}</Link>
                    {' · '}
                    <Link href="/rksv/verifications">{t('users.list.forensicsLinkVerifications')}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            <UsersPageActiveTenantContext />

            {isSuperAdminLayout ? (
                <UnifiedAdminUsersView
                    policy={policy}
                    roleDisplayLabel={roleDisplayLabel}
                    currentUserId={currentUser?.id}
                    onView={setDetailUser}
                    onEdit={(id) => setEditUserId(id)}
                    onDeactivate={setDeactivateUserRecord}
                    onReactivate={setReactivateUserRecord}
                    onResetPassword={setResetPasswordUser}
                    onManagePermissions={policy.canManagePermissions ? setPermissionsUser : undefined}
                    onCreatePlatformUser={() => {
                        setCreatePlatformMode(true);
                        setCreateOpen(true);
                    }}
                />
            ) : currentTenantId ? (
                <UnifiedAdminUsersView
                    policy={policy}
                    roleDisplayLabel={roleDisplayLabel}
                    currentUserId={currentUser?.id}
                    tenantScopeId={currentTenantId}
                    isSuperAdminActor={false}
                    onView={setDetailUser}
                    onEdit={(id) => setEditUserId(id)}
                    onDeactivate={setDeactivateUserRecord}
                    onReactivate={setReactivateUserRecord}
                    onResetPassword={setResetPasswordUser}
                    onManagePermissions={policy.canManagePermissions ? setPermissionsUser : undefined}
                    onCreatePlatformUser={() => {}}
                />
            ) : null}

            {createOpen ? (
                <UserFormDrawer
                    open
                    onClose={() => {
                        setCreateOpen(false);
                        setCreatePlatformMode(false);
                    }}
                    mode="create"
                    createVariant={createPlatformMode ? 'platform' : 'default'}
                    roleOptions={roleOptions}
                    rolesLoading={rolesLoading}
                    onSubmit={handleCreate}
                    loading={createMutation.isPending}
                />
            ) : null}
            {editUserId ? (
                <UserFormDrawer
                    key={editUserId}
                    open
                    onClose={() => setEditUserId(null)}
                    mode="edit"
                    user={editUserFull ?? undefined}
                    initialLoading={editUserLoading}
                    fetchError={editUserError ? (editUserFetchError ?? null) : null}
                    onRetryFetch={refetchEditUser}
                    roleOptions={roleOptions}
                    rolesLoading={rolesLoading}
                    onSubmit={handleEdit}
                    loading={updateMutation.isPending || updateUserTenantsMutation.isPending || updateUserRoleMutation.isPending}
                    canManageTenants={isSuperAdminLayout}
                />
            ) : null}

            <UserDetailDrawer
                open={!!detailUser}
                onClose={() => setDetailUser(null)}
                user={detailUser}
                canEditUsername={policy.canEdit}
                onUsernameUpdated={handleUsernameUpdated}
            />

            {usernameEditUser?.id ? (
                <EditUsernameModal
                    open={!!usernameEditUser}
                    userId={usernameEditUser.id}
                    currentUsername={usernameEditUser.userName ?? ''}
                    userEmail={usernameEditUser.email}
                    onClose={() => setUsernameEditUser(null)}
                    onSuccess={(result) => {
                        handleUsernameUpdated(usernameEditUser.id!, result);
                        setUsernameEditUser(null);
                    }}
                />
            ) : null}

            {deactivateUserRecord ? (
                <DeactivateUserModal
                    user={deactivateUserRecord}
                    onCancel={() => setDeactivateUserRecord(null)}
                    onConfirm={handleDeactivateConfirm}
                    confirmLoading={deactivateMutation.isPending}
                    reasonRules={modalRules.reason}
                />
            ) : null}

            <Modal
                title={t('users.reactivate.title')}
                open={!!reactivateUserRecord}
                onOk={handleReactivate}
                onCancel={() => setReactivateUserRecord(null)}
                okText={t('users.reactivate.confirm')}
                confirmLoading={reactivateMutation.isPending}
                destroyOnHidden
            >
                {reactivateUserRecord && (
                    <p>
                        <strong>{fullName(reactivateUserRecord)}</strong> {t('users.reactivate.confirmBody')}
                    </p>
                )}
            </Modal>

            {resetPasswordUser ? (
                policy.useGeneratedPasswordReset ? (
                    <ResetPasswordModal
                        open
                        user={resetPasswordUser}
                        onClose={() => setResetPasswordUser(null)}
                        onSuccess={() => {
                            invalidateAllUserLists();
                            if (resetPasswordUser.id) {
                                queryClient.invalidateQueries({ queryKey: [`/api/AuditLog/user/${resetPasswordUser.id}`] });
                            }
                        }}
                    />
                ) : (
                    <ResetPasswordUserModal
                        user={resetPasswordUser}
                        onCancel={() => {
                            setResetPasswordUser(null);
                            setResetPasswordValidationError(null);
                        }}
                        onConfirm={handleResetPasswordConfirm}
                        confirmLoading={resetPasswordMutation.isPending}
                        passwordRules={modalRules.newPassword}
                        validationError={resetPasswordValidationError}
                        onClearValidationError={handleClearResetPasswordValidationError}
                    />
                )
            ) : null}

            {createRoleOpen ? (
                <CreateRoleModal
                    onCancel={() => setCreateRoleOpen(false)}
                    onConfirm={handleCreateRoleConfirm}
                    confirmLoading={createRoleMutation.isPending}
                    roleNameRules={modalRules.roleName}
                    inheritRoleOptions={roleOptions}
                />
            ) : null}

            {permissionsUser?.id ? (
                <UserPermissionsModal
                    open
                    userId={permissionsUser.id}
                    userName={fullName(permissionsUser)}
                    userRole={permissionsUser.role}
                    onClose={() => setPermissionsUser(null)}
                />
            ) : null}
        </AdminPageShell>
    );
}
