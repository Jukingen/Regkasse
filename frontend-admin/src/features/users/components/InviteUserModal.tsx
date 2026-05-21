'use client';

import React, { useEffect, useMemo } from 'react';
import { Alert, Form, Input, Modal, Select, Switch, Typography } from 'antd';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { INVITE_TENANT_ROLES } from '@/features/super-admin/api/tenantUsers';
import { InviteTenantContextBanner } from '@/features/users/components/InviteTenantContextBanner';
import { TenantSelector } from '@/features/users/components/TenantSelector';
import type { InviteTenantContextFields } from '@/features/users/utils/inviteTenantDisplay';
import { useI18n } from '@/i18n';

export type InviteUserFormValues = {
    email: string;
    role: string;
    isOwner: boolean;
    tenantId?: string;
};

export type InviteUserModalProps = {
    open: boolean;
    confirmLoading?: boolean;
    onClose: () => void;
    onSubmit: (values: InviteUserFormValues) => void;
    /** Fixed tenant from tenant detail — hides selector, shows confirmation banner */
    tenantId?: string;
    /** @deprecated Use tenantId */
    fixedTenantId?: string;
    tenantContext?: InviteTenantContextFields | null;
    /** Tenants for dropdown (Super Admin / users page when no fixed tenantId) */
    tenantRows?: AdminTenantListItem[];
    tenantsLoading?: boolean;
    showOwnerToggle?: boolean;
    variant?: 'tenantDetail' | 'usersPage';
    initialValues?: Partial<InviteUserFormValues>;
};

const USERS_PAGE_ROLES = ['Manager', 'Cashier', 'Accountant'] as const;

export function InviteUserModal({
    open,
    confirmLoading,
    onClose,
    onSubmit,
    tenantId: tenantIdProp,
    fixedTenantId,
    tenantContext,
    tenantRows = [],
    tenantsLoading = false,
    showOwnerToggle = false,
    variant = 'usersPage',
    initialValues,
}: InviteUserModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<InviteUserFormValues>();
    const isUsersPage = variant === 'usersPage';
    const resolvedFixedTenantId = tenantIdProp ?? fixedTenantId;
    const selectedTenantId = Form.useWatch('tenantId', form);

    const tenantById = useMemo(() => new Map(tenantRows.map((row) => [row.id, row])), [tenantRows]);

    const roleOptions = useMemo(() => {
        const roles = isUsersPage ? USERS_PAGE_ROLES : INVITE_TENANT_ROLES;
        return roles.map((role) => ({
            value: role,
            label: isUsersPage ? t(`users.invite.roleOptions.${role}.label`) : role,
        }));
    }, [isUsersPage, t]);

    const fixedContext =
        tenantContext ?? (resolvedFixedTenantId ? tenantById.get(resolvedFixedTenantId) : null) ?? null;

    const showTenantSelector = !resolvedFixedTenantId && tenantRows.length > 0;

    const selectedContext = useMemo(() => {
        if (fixedContext) {
            return fixedContext;
        }
        if (selectedTenantId) {
            return tenantById.get(selectedTenantId) ?? null;
        }
        return null;
    }, [fixedContext, selectedTenantId, tenantById]);

    useEffect(() => {
        if (!open) {
            form.resetFields();
            return;
        }
        form.setFieldsValue({
            role: 'Manager',
            isOwner: false,
            tenantId: resolvedFixedTenantId,
            ...initialValues,
        });
    }, [open, form, resolvedFixedTenantId, initialValues]);

    const handleFinish = (values: InviteUserFormValues) => {
        const tenantId = resolvedFixedTenantId ?? values.tenantId;
        onSubmit({
            ...values,
            tenantId,
            isOwner: showOwnerToggle ? Boolean(values.isOwner) : false,
        });
    };

    const modalTitle =
        fixedContext
            ? variant === 'tenantDetail'
                ? t('users.invite.titleAddForTenant', { name: fixedContext.name, slug: fixedContext.slug })
                : t('users.invite.titleForTenant', { name: fixedContext.name, slug: fixedContext.slug })
            : t('users.invite.title');

    return (
        <Modal
            title={modalTitle}
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            okText={t('users.invite.createUser')}
            cancelText={t('common.buttons.cancel')}
            confirmLoading={confirmLoading}
            destroyOnClose
            width={showTenantSelector ? 560 : undefined}
        >
            <Form form={form} layout="vertical" onFinish={handleFinish}>
                {selectedContext ? <InviteTenantContextBanner tenant={selectedContext} variant="modal" /> : null}
                <Form.Item
                    name="email"
                    label={t('users.invite.email')}
                    rules={[
                        { required: true, message: t('tenants.users.invite.emailRequired') },
                        { type: 'email', message: t('tenants.users.invite.emailInvalid') },
                    ]}
                >
                    <Input type="email" placeholder="neuer@mitarbeiter.cafe.at" />
                </Form.Item>
                {showTenantSelector ? (
                    <Form.Item
                        name="tenantId"
                        label={t('users.invite.tenant')}
                        rules={[{ required: true, message: t('users.invite.tenantRequired') }]}
                    >
                        <TenantSelector
                            tenants={tenantRows}
                            loading={tenantsLoading}
                            placeholder={t('users.invite.tenantPlaceholder')}
                        />
                    </Form.Item>
                ) : null}
                <Form.Item name="role" label={t('users.invite.role')} rules={[{ required: true }]}>
                    <Select options={roleOptions} />
                </Form.Item>
                <Alert type="info" showIcon style={{ marginBottom: 16 }} message={t('tenants.users.invite.createHint')} />
                {showOwnerToggle ? (
                    <Form.Item name="isOwner" label={t('tenants.users.invite.isOwner')} valuePropName="checked">
                        <Switch />
                    </Form.Item>
                ) : null}
            </Form>
        </Modal>
    );
}
