'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { App, Modal, Alert, Button, Typography } from 'antd';
import { useCallback, useMemo, useState } from 'react';

import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import { CreateUserModal, type CreateUserFormValues } from '@/features/super-admin/components/CreateUserModal';
import {
    applyTenantImpersonationSession,
    impersonateAdminTenant,
} from '@/features/super-admin/api/adminTenants';
import { useCreateUser } from '@/features/users/hooks/useCreateUser';
import { getApiAdminTenantsQueryKey } from '@/features/tenancy/api/getApiAdminTenants';
import type { TenantListItemForSwitcher } from '@/features/tenancy/hooks/useTenantListForSwitcher';
import { persistTenantSlugAndRefresh } from '@/features/tenancy/services/setTenantAndRefresh';
import { useI18n } from '@/i18n';

export type TenantSwitcherNoAdminFlowProps = {
    tenant: TenantListItemForSwitcher | null;
    open: boolean;
    onClose: () => void;
    onCompleted?: () => void;
};

export function TenantSwitcherNoAdminFlow({
    tenant,
    open,
    onClose,
    onCompleted,
}: TenantSwitcherNoAdminFlowProps) {
  const { message } = App.useApp();

    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [createOpen, setCreateOpen] = useState(false);
    const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);

    const defaultAdminEmail = useMemo(
        () => (tenant ? `admin@${tenant.slug}.regkasse.at` : ''),
        [tenant],
    );

    const createInitialValues = useMemo(
        (): Partial<CreateUserFormValues> => ({
            email: defaultAdminEmail,
            role: 'Manager',
            isOwner: true,
        }),
        [defaultAdminEmail],
    );

    const closeAll = useCallback(() => {
        setCreateOpen(false);
        onClose();
    }, [onClose]);

    const impersonateMutation = useMutation({
        mutationFn: (tenantId: string) => impersonateAdminTenant(tenantId),
        onSuccess: (res) => {
            setImpersonationRedirecting(true);
            closeAll();
            onCompleted?.();
            applyTenantImpersonationSession(res);
        },
        onError: () => message.error(t('tenants.messages.impersonationFailed')),
    });

    const createMutation = useCreateUser({
        fixedTenantId: tenant?.id,
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: getApiAdminTenantsQueryKey(false) });
        },
    });

    const handleCreateComplete = useCallback(() => {
        setCreateOpen(false);
        closeAll();
        onCompleted?.();
        if (tenant) {
            persistTenantSlugAndRefresh(tenant.slug);
        }
    }, [closeAll, onCompleted, tenant]);

    const handleSwitchDev = useCallback(() => {
        if (!tenant) return;
        closeAll();
        onCompleted?.();
        persistTenantSlugAndRefresh(tenant.slug);
    }, [tenant, closeAll, onCompleted]);

    return (
        <>
            {impersonationRedirecting ? <ImpersonationRedirectOverlay /> : null}
            <Modal
                title={t('adminShell.tenant.devSwitcher.noAdminSwitchTitle')}
                open={open && !createOpen}
                onCancel={closeAll}
                footer={[
                    <Button key="cancel" onClick={closeAll}>
                        {t('common.buttons.cancel')}
                    </Button>,
                    <Button key="dev" onClick={handleSwitchDev}>
                        {t('adminShell.tenant.devSwitcher.switchAnywayDev')}
                    </Button>,
                    <Button key="create" onClick={() => setCreateOpen(true)}>
                        {t('adminShell.tenant.devSwitcher.createAdmin')}
                    </Button>,
                    <Button
                        key="impersonate"
                        type="primary"
                        loading={impersonateMutation.isPending}
                        disabled={!tenant}
                        onClick={() => tenant && impersonateMutation.mutate(tenant.id)}
                    >
                        {t('adminShell.tenant.devSwitcher.switchAsSuperAdmin')}
                    </Button>,
                ]}
            >
                <Typography.Paragraph style={{ marginBottom: 12 }}>
                    {t('adminShell.tenant.devSwitcher.noAdminSwitchMessage')}
                </Typography.Paragraph>
                {tenant ? (
                    <Alert
                        type="warning"
                        showIcon
                        title={`${tenant.statusIcon} ${tenant.name} (${tenant.slug})`}
                        description={t('adminShell.tenant.devSwitcher.noAdmin')}
                    />
                ) : null}
            </Modal>
            {createOpen && tenant ? (
                <CreateUserModal
                    open
                    variant="tenantDetail"
                    tenantId={tenant.id}
                    showOwnerToggle
                    confirmLoading={createMutation.isPending}
                    onClose={() => setCreateOpen(false)}
                    onComplete={handleCreateComplete}
                    onSubmit={(values) => createMutation.mutateAsync(values)}
                    initialValues={createInitialValues}
                />
            ) : null}
        </>
    );
}
