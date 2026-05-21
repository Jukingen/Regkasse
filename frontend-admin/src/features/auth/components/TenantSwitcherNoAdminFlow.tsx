'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Modal, Typography, message } from 'antd';
import { useCallback, useMemo, useState } from 'react';

import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import { InviteUserModal, type InviteUserFormValues } from '@/features/super-admin/components/InviteUserModal';
import {
    applyTenantImpersonationSession,
    impersonateAdminTenant,
} from '@/features/super-admin/api/adminTenants';
import { inviteTenantUser } from '@/features/super-admin/api/tenantUsers';
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
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [inviteOpen, setInviteOpen] = useState(false);
    const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);

    const defaultAdminEmail = useMemo(
        () => (tenant ? `admin@${tenant.slug}.regkasse.at` : ''),
        [tenant],
    );

    const inviteInitialValues = useMemo(
        (): Partial<InviteUserFormValues> => ({
            email: defaultAdminEmail,
            role: 'Manager',
            isOwner: true,
        }),
        [defaultAdminEmail],
    );

    const closeAll = useCallback(() => {
        setInviteOpen(false);
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

    const inviteMutation = useMutation({
        mutationFn: (values: InviteUserFormValues) =>
            inviteTenantUser(tenant!.id, {
                email: values.email,
                role: values.role,
                isOwner: values.isOwner,
            }),
        onSuccess: (result) => {
            void queryClient.invalidateQueries({ queryKey: getApiAdminTenantsQueryKey(false) });
            if (result.userCreated) {
                message.success(t('tenants.users.invite.messages.sent'));
            } else {
                message.warning(t('tenants.users.invite.messages.assignedNoEmail'));
            }
            setInviteOpen(false);
            closeAll();
            onCompleted?.();
            if (tenant) {
                persistTenantSlugAndRefresh(tenant.slug);
            }
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
    });

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
                open={open && !inviteOpen}
                onCancel={closeAll}
                footer={[
                    <Button key="cancel" onClick={closeAll}>
                        {t('common.buttons.cancel')}
                    </Button>,
                    <Button key="dev" onClick={handleSwitchDev}>
                        {t('adminShell.tenant.devSwitcher.switchAnywayDev')}
                    </Button>,
                    <Button key="create" onClick={() => setInviteOpen(true)}>
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
                        message={`${tenant.statusIcon} ${tenant.name} (${tenant.slug})`}
                        description={t('adminShell.tenant.devSwitcher.noAdmin')}
                    />
                ) : null}
            </Modal>
            <InviteUserModal
                open={inviteOpen}
                confirmLoading={inviteMutation.isPending}
                onClose={() => setInviteOpen(false)}
                onSubmit={(values) => inviteMutation.mutate(values)}
                initialValues={inviteInitialValues}
            />
        </>
    );
}
