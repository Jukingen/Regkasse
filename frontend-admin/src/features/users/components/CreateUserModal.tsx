'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Form, Input, Modal, Select, Space, Switch, Tabs, Typography, message } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { QUICK_USER_ROLES, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import type { CreateUserResult } from '@/features/users/api/users';
import { TENANT_CREATE_ROLES } from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';

export type CreateUserFormValues = {
    email: string;
    firstName?: string;
    lastName?: string;
    role: string;
    isOwner: boolean;
    tenantId?: string;
};

export type CreateUserQuickFormValues = {
    role: string;
    tenantId?: string;
};

export type CreateUserModalProps = {
    open: boolean;
    confirmLoading?: boolean;
    onClose: () => void;
    /** Called after the one-time password modal is dismissed. */
    onComplete?: () => void;
    /** Must resolve with create result (generated password) on success. */
    onSubmit: (values: CreateUserFormValues) => Promise<CreateUserResult>;
    /** Super Admin: show mandant picker when no fixed tenantId. */
    isSuperAdmin?: boolean;
    tenantId?: string;
    tenantRows?: AdminTenantListItem[];
    tenantsLoading?: boolean;
    showOwnerToggle?: boolean;
    variant?: 'tenantDetail' | 'usersPage';
    initialValues?: Partial<CreateUserFormValues>;
    quickMode?: {
        onSubmit: (values: CreateUserQuickFormValues) => Promise<CreateQuickUserResult>;
    };
};

const ROLE_I18N_KEYS: Record<string, string> = {
    Manager: 'users.create.roleOptions.Manager.label',
    Cashier: 'users.create.roleOptions.Cashier.label',
    Accountant: 'users.create.roleOptions.Accountant.label',
    Waiter: 'users.create.roleOptions.Waiter.label',
    Kitchen: 'users.create.roleOptions.Kitchen.label',
};

export function CreateUserModal({
    open,
    confirmLoading = false,
    onClose,
    onComplete,
    onSubmit,
    isSuperAdmin = false,
    tenantId: fixedTenantId,
    tenantRows = [],
    tenantsLoading = false,
    showOwnerToggle = false,
    variant = 'usersPage',
    initialValues,
    quickMode,
}: CreateUserModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<CreateUserFormValues>();
    const [quickForm] = Form.useForm<CreateUserQuickFormValues>();
    const [passwordResult, setPasswordResult] = useState<CreateUserResult | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
    const [quickSubmitting, setQuickSubmitting] = useState(false);
    const [activeTab, setActiveTab] = useState<'normal' | 'quick'>('normal');

    const showTenantPicker = isSuperAdmin && !fixedTenantId;

    const roleOptions = useMemo(
        () =>
            TENANT_CREATE_ROLES.map((role) => ({
                value: role,
                label: t(ROLE_I18N_KEYS[role] ?? role, { defaultValue: role }),
            })),
        [t],
    );

    const quickRoleOptions = useMemo(
        () =>
            QUICK_USER_ROLES.map((role) => ({
                value: role,
                label: t(ROLE_I18N_KEYS[role] ?? role, { defaultValue: role }),
            })),
        [t],
    );

    const tenantOptions = useMemo(
        () =>
            tenantRows.map((row) => ({
                value: row.id,
                label: t('users.create.tenantOption', { name: row.name, slug: row.slug }),
            })),
        [tenantRows, t],
    );

    const tenantById = useMemo(() => new Map(tenantRows.map((row) => [row.id, row])), [tenantRows]);

    const modalTitle = useMemo(() => {
        if (fixedTenantId) {
            const tenant = tenantById.get(fixedTenantId);
            if (tenant) {
                return variant === 'tenantDetail'
                    ? t('users.create.titleAddForTenant', { name: tenant.name, slug: tenant.slug })
                    : t('users.create.titleForTenant', { name: tenant.name, slug: tenant.slug });
            }
        }
        return t('users.create.title');
    }, [fixedTenantId, tenantById, variant, t]);

    useEffect(() => {
        if (!open) {
            form.resetFields();
            quickForm.resetFields();
            setActiveTab('normal');
            return;
        }
        form.setFieldsValue({
            role: 'Manager',
            isOwner: false,
            tenantId: fixedTenantId,
            ...initialValues,
        });
        quickForm.setFieldsValue({
            role: 'Manager',
            ...(fixedTenantId ? { tenantId: fixedTenantId } : {}),
        });
    }, [open, form, quickForm, fixedTenantId, initialValues]);

    const watchedQuickRole = Form.useWatch('role', quickForm) ?? 'Manager';
    const watchedQuickTenantId = Form.useWatch('tenantId', quickForm) ?? fixedTenantId;
    const quickPreviewTenant = watchedQuickTenantId ? tenantById.get(watchedQuickTenantId) : undefined;
    const quickPreviewSlug = quickPreviewTenant?.slug ?? 'tenant';
    const quickPreviewName = quickPreviewTenant?.name ?? fixedTenantId ?? '';
    const infoEmailExample = t('tenants.users.quick.emailPreview', {
        role: watchedQuickRole.toLowerCase(),
        random: 'a3f9k2',
        slug: quickPreviewSlug,
    });

    const handleClose = () => {
        setPasswordResult(null);
        setQuickResult(null);
        setActiveTab('normal');
        onClose();
    };

    const handleFinish = async (values: CreateUserFormValues) => {
        const tenantId = fixedTenantId ?? values.tenantId;
        if (!tenantId) {
            form.setFields([{ name: 'tenantId', errors: [t('users.create.tenantRequired')] }]);
            return;
        }
        setSubmitting(true);
        try {
            const result = await onSubmit({
                ...values,
                email: values.email.trim(),
                firstName: values.firstName?.trim() || undefined,
                lastName: values.lastName?.trim() || undefined,
                tenantId,
                isOwner: showOwnerToggle ? Boolean(values.isOwner) : false,
            });
            if (result?.success && result.generatedPassword) {
                setPasswordResult(result);
            }
        } catch {
            /* parent shows error toast */
        } finally {
            setSubmitting(false);
        }
    };

    const handleQuickFinish = async (values: CreateUserQuickFormValues) => {
        if (!quickMode) return;
        const tenantId = fixedTenantId ?? values.tenantId;
        if (!tenantId) {
            quickForm.setFields([{ name: 'tenantId', errors: [t('users.create.tenantRequired')] }]);
            return;
        }
        setQuickSubmitting(true);
        try {
            const result = await quickMode.onSubmit({
                role: values.role,
                tenantId,
            });
            if (result?.success) {
                setQuickResult(result);
            }
        } catch {
            /* parent shows error toast */
        } finally {
            setQuickSubmitting(false);
        }
    };

    const copyPassword = async () => {
        if (!passwordResult?.generatedPassword) return;
        try {
            await navigator.clipboard.writeText(passwordResult.generatedPassword);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    const closePasswordModal = () => {
        setPasswordResult(null);
        onComplete?.();
        onClose();
    };

    const closeQuickResult = () => {
        setQuickResult(null);
        onComplete?.();
        onClose();
    };

    const handleGenerateAnotherQuickUser = () => {
        setQuickResult(null);
        setActiveTab('quick');
        quickForm.setFieldsValue({
            role: 'Manager',
            ...(fixedTenantId ? { tenantId: fixedTenantId } : {}),
        });
    };

    const loading = activeTab === 'quick' ? quickSubmitting : confirmLoading || submitting;

    const normalForm = (
        <Form form={form} layout="vertical" onFinish={handleFinish}>
            <Form.Item
                name="email"
                label={t('users.create.email')}
                rules={[
                    { required: true, message: t('users.create.emailRequired') },
                    { type: 'email', message: t('users.create.emailInvalid') },
                ]}
            >
                <Input type="email" placeholder="benutzer@firma.at" />
            </Form.Item>

            <Form.Item name="firstName" label={t('users.create.firstName')}>
                <Input placeholder="Max" maxLength={50} />
            </Form.Item>

            <Form.Item name="lastName" label={t('users.create.lastName')}>
                <Input placeholder="Mustermann" maxLength={50} />
            </Form.Item>

            <Form.Item name="role" label={t('users.create.role')} rules={[{ required: true }]}>
                <Select options={roleOptions} />
            </Form.Item>

            {showTenantPicker ? (
                <Form.Item
                    name="tenantId"
                    label={t('users.create.tenant')}
                    rules={[{ required: true, message: t('users.create.tenantRequired') }]}
                >
                    <Select
                        showSearch
                        optionFilterProp="label"
                        placeholder={t('users.create.tenantPlaceholder')}
                        loading={tenantsLoading}
                        options={tenantOptions}
                    />
                </Form.Item>
            ) : null}

            {showOwnerToggle ? (
                <Form.Item name="isOwner" label={t('users.create.isOwner')} valuePropName="checked">
                    <Switch />
                </Form.Item>
            ) : null}
        </Form>
    );

    const quickFormContent = quickMode ? (
        <Form form={quickForm} layout="vertical" onFinish={handleQuickFinish}>
            {showTenantPicker ? (
                <Form.Item
                    name="tenantId"
                    label={t('users.create.tenant')}
                    rules={[{ required: true, message: t('users.create.tenantRequired') }]}
                >
                    <Select
                        showSearch
                        optionFilterProp="label"
                        placeholder={t('users.create.tenantPlaceholder')}
                        loading={tenantsLoading}
                        options={tenantOptions}
                    />
                </Form.Item>
            ) : null}

            <Form.Item name="role" label={t('tenants.users.quick.role')} rules={[{ required: true }]}>
                <Select options={quickRoleOptions} />
            </Form.Item>

            <Alert
                type="info"
                showIcon
                message={t('tenants.users.quick.autoTitle')}
                description={
                    <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
                        <li>{infoEmailExample}</li>
                        <li>{t('tenants.users.quick.autoPassword')}</li>
                        <li>{t('tenants.users.quick.autoForceChange')}</li>
                    </ul>
                }
            />
        </Form>
    ) : null;

    return (
        <>
            <Modal
                title={modalTitle}
                open={open && !passwordResult && !quickResult}
                onCancel={handleClose}
                width={600}
                destroyOnClose
                footer={[
                    <Button key="cancel" onClick={handleClose}>
                        {t('common.buttons.cancel')}
                    </Button>,
                    <Button
                        key="submit"
                        type="primary"
                        loading={loading}
                        onClick={() => {
                            if (activeTab === 'quick' && quickMode) {
                                quickForm.submit();
                                return;
                            }
                            form.submit();
                        }}
                    >
                        {activeTab === 'quick' && quickMode ? t('tenants.users.quick.generate') : t('users.create.submit')}
                    </Button>,
                ]}
            >
                {quickMode ? (
                    <Tabs
                        activeKey={activeTab}
                        onChange={(key) => setActiveTab(key as 'normal' | 'quick')}
                        items={[
                            {
                                key: 'normal',
                                label: t('users.create.tabs.normal'),
                                children: normalForm,
                            },
                            {
                                key: 'quick',
                                label: t('users.create.tabs.quick'),
                                children: quickFormContent,
                            },
                        ]}
                    />
                ) : (
                    normalForm
                )}
            </Modal>

            <Modal
                title={t('users.create.success')}
                open={!!passwordResult}
                onCancel={closePasswordModal}
                destroyOnClose
                footer={[
                    <Button key="copy" type="primary" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                        {t('users.create.copyPassword')}
                    </Button>,
                    <Button key="close" onClick={closePasswordModal}>
                        {t('users.create.close')}
                    </Button>,
                ]}
            >
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text>{passwordResult?.email}</Typography.Text>
                    <Alert
                        type="warning"
                        showIcon
                        message={t('users.create.passwordWarningTitle')}
                        description={t('users.create.generatedPasswordInfo')}
                    />
                    <Input.Password value={passwordResult?.generatedPassword ?? ''} readOnly />
                </Space>
            </Modal>

            {quickMode ? (
                <QuickUserSuccessModal
                    open={!!quickResult}
                    result={quickResult}
                    role={watchedQuickRole}
                    tenantName={quickPreviewName}
                    tenantSlug={quickPreviewSlug}
                    onClose={closeQuickResult}
                    onGenerateAnother={handleGenerateAnotherQuickUser}
                />
            ) : null}
        </>
    );
}
