'use client';

import React, { useState } from 'react';
import { Alert, Button, Modal, Space, Typography, message } from 'antd';
import { MailOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { InviteUserModal } from '@/features/users/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/users/components/InviteUserModal';
import {
    adminUsersQueryKeys,
    inviteAdminUser,
    type TenantUserInviteResult,
} from '@/features/users/api/users';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useI18n } from '@/i18n';

/** Dedicated invitations tab — tenant dropdown with domain and license (Super Admin). */
export function UserInvitationsPanel() {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [inviteOpen, setInviteOpen] = useState(false);
    const [inviteResult, setInviteResult] = useState<TenantUserInviteResult | null>(null);

    const { tenants, isLoading, isSuperAdmin } = useTenantList();

    const inviteMutation = useMutation({
        mutationFn: (values: InviteUserFormValues) => {
            if (!values.tenantId) throw new Error('tenantId required');
            return inviteAdminUser({
                email: values.email,
                tenantId: values.tenantId,
                role: values.role,
                isOwner: values.isOwner,
            });
        },
        onSuccess: (res) => {
            setInviteResult(res);
            setInviteOpen(false);
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
            if (res.invitationEmailSent) {
                message.success(t('tenants.users.invite.messages.sent'));
            } else {
                message.warning(t('tenants.users.invite.messages.assignedNoEmail'));
            }
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
    });

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {isSuperAdmin
                    ? t('users.tabs.invitations.descriptionSuperAdmin')
                    : t('users.tabs.invitations.description')}
            </Typography.Paragraph>
            <Alert type="info" showIcon message={t('users.tabs.invitations.smtpHint')} />
            <Button type="primary" icon={<MailOutlined />} onClick={() => setInviteOpen(true)}>
                {t('users.invite.action')}
            </Button>
            <InviteUserModal
                open={inviteOpen}
                variant="usersPage"
                tenantRows={tenants}
                tenantsLoading={isLoading}
                confirmLoading={inviteMutation.isPending}
                onClose={() => setInviteOpen(false)}
                onSubmit={(values) => inviteMutation.mutate(values)}
            />
            <Modal
                title={t('tenants.users.invite.resultTitle')}
                open={!!inviteResult}
                onCancel={() => setInviteResult(null)}
                footer={[
                    <Button key="close" onClick={() => setInviteResult(null)}>
                        {t('common.buttons.close')}
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
                    </Space>
                ) : null}
            </Modal>
        </Space>
    );
}
