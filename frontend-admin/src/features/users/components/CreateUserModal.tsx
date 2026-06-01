'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { App, Modal, Alert, Button, Form, Input, Select, Space, Switch, Tabs, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { CredentialCopyRow } from '@/features/super-admin/components/CredentialCopyRow';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { QUICK_USER_ROLES, type CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { getQuickUsernamePattern } from '@/features/super-admin/lib/quickUserPreview';
import { fetchUsernameSuggestion, type CreateUserResult } from '@/features/users/api/users';
import { TENANT_CREATE_ROLES } from '@/features/super-admin/api/tenantUsers';
import { TenantSelector } from '@/features/users/components/TenantSelector';
import { UserTenantAssignmentModal } from '@/features/users/components/UserTenantAssignmentModal';
import { useTenantAssignmentModal } from '@/features/users/hooks/useTenantAssignmentModal';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

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
    allowDeferredTenantAssignment?: boolean;
    onAssignTenants?: (userId: string, tenantIds: string[]) => Promise<void>;
    quickMode?: {
        onSubmit: (values: CreateUserQuickFormValues) => Promise<CreateQuickUserResult>;
        onSubmitWithoutTenant?: (values: CreateUserQuickFormValues) => Promise<CreateUserResult>;
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
    allowDeferredTenantAssignment = false,
    onAssignTenants,
    quickMode,
}: CreateUserModalProps) {
  const { message } = App.useApp();

    const { t } = useI18n();
    const [form] = Form.useForm<CreateUserFormValues>();
    const [quickForm] = Form.useForm<CreateUserQuickFormValues>();
    const [passwordResult, setPasswordResult] = useState<CreateUserResult | null>(null);
    const [password, setPassword] = useState('');
    const [tenantAssignmentResult, setTenantAssignmentResult] = useState<CreateUserResult | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [assignmentSubmitting, setAssignmentSubmitting] = useState(false);
    const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
    const [quickSubmitting, setQuickSubmitting] = useState(false);
    const [activeTab, setActiveTab] = useState<'normal' | 'quick'>('normal');
    const tenantAssignmentModal = useTenantAssignmentModal();

    const showTenantPicker = isSuperAdmin && !fixedTenantId;
    const canDeferTenantAssignment = showTenantPicker && !fixedTenantId && allowDeferredTenantAssignment && !!onAssignTenants;
    const canDeferQuickTenantAssignment =
        showTenantPicker && !fixedTenantId && allowDeferredTenantAssignment && !!onAssignTenants && !!quickMode?.onSubmitWithoutTenant;

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

    const tenantById = useMemo(() => new Map(tenantRows.map((row) => [row.id, row])), [tenantRows]);

    /** Mandant list for create only (parent `useTenantList`); not the header dev tenant switcher. */
    const createUserTenantField = showTenantPicker ? (
        <Form.Item
            name="tenantId"
            label={t('users.create.tenant')}
            rules={canDeferTenantAssignment ? [] : [{ required: true, message: t('users.create.tenantRequired') }]}
        >
            <TenantSelector tenants={tenantRows} loading={tenantsLoading} />
        </Form.Item>
    ) : null;

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
            setPassword('');
            setTenantAssignmentResult(null);
            tenantAssignmentModal.closeModal();
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

    useEffect(() => {
        if (passwordResult?.generatedPassword) {
            setPassword(passwordResult.generatedPassword);
            return;
        }

        setPassword('');
    }, [passwordResult]);

    const watchedQuickRole = Form.useWatch('role', quickForm) ?? 'Manager';
    const watchedQuickTenantId = Form.useWatch('tenantId', quickForm) ?? fixedTenantId;
    const quickPreviewTenant = watchedQuickTenantId ? tenantById.get(watchedQuickTenantId) : undefined;
    const quickPreviewSlug = quickPreviewTenant?.slug ?? (canDeferQuickTenantAssignment ? 'platform' : 'tenant');
    const quickPreviewName = quickPreviewTenant?.name ?? fixedTenantId ?? '';
    const quickUsernamePattern = getQuickUsernamePattern(watchedQuickRole);
    const { data: usernameSuggestion } = useQuery({
        queryKey: ['admin', 'username-suggestion', watchedQuickRole],
        queryFn: () => fetchUsernameSuggestion(watchedQuickRole),
        enabled: open && activeTab === 'quick' && Boolean(watchedQuickRole),
        staleTime: 30_000,
    });
    const quickUsernameAlternates = useMemo(() => {
        if (!usernameSuggestion?.availableNumbers?.length) return null;
        const prefix = usernameSuggestion.suggestedUsername.replace(/\d+$/, '');
        return usernameSuggestion.availableNumbers.map((n) => `${prefix}${n}`).join(', ');
    }, [usernameSuggestion]);
    const quickEmailPreview = t('tenants.users.quick.emailPreview', {
        role: watchedQuickRole.toLowerCase(),
        random: 'a3f9k2',
        slug: quickPreviewSlug,
    });

    const handleClose = () => {
        setPasswordResult(null);
        setTenantAssignmentResult(null);
        tenantAssignmentModal.closeModal();
        setQuickResult(null);
        setActiveTab('normal');
        onClose();
    };

    const handleFinish = async (values: CreateUserFormValues) => {
        const tenantId = fixedTenantId ?? values.tenantId;
        if (!tenantId && !canDeferTenantAssignment) {
            form.setFields([{ name: 'tenantId', errors: [t('users.create.tenantRequired')] }]);
            return;
        }
        setSubmitting(true);
        const usedDeferredCreate = canDeferTenantAssignment && !tenantId;
        const submitPayload: CreateUserFormValues = {
            email: values.email.trim(),
            firstName: values.firstName?.trim() || undefined,
            lastName: values.lastName?.trim() || undefined,
            role: values.role,
            isOwner: showOwnerToggle ? Boolean(values.isOwner) : false,
            ...(usedDeferredCreate ? {} : tenantId ? { tenantId } : {}),
        };
        try {
            const result = await onSubmit(submitPayload);
            if (result?.success && result.generatedPassword) {
                if (usedDeferredCreate && onAssignTenants) {
                    if (tenantId) {
                        try {
                            await onAssignTenants(result.userId, [tenantId]);
                            setPasswordResult(result);
                        } catch {
                            setTenantAssignmentResult(result);
                            tenantAssignmentModal.openModal({
                                userId: result.userId,
                                userEmail: values.email.trim(),
                                initialSelectedTenantIds: [tenantId],
                            });
                        }
                        return;
                    }
                    setTenantAssignmentResult(result);
                    tenantAssignmentModal.openModal({
                        userId: result.userId,
                        userEmail: values.email.trim(),
                        initialSelectedTenantIds: [],
                    });
                    return;
                }
                setPasswordResult(result);
            }
        } catch {
            /* parent shows error toast */
        } finally {
            setSubmitting(false);
        }
    };

    const handleTenantAssignmentSave = async (selectedTenantIds: string[]) => {
        if (!tenantAssignmentModal.userId || !tenantAssignmentResult || !onAssignTenants) return;
        setAssignmentSubmitting(true);
        try {
            await onAssignTenants(tenantAssignmentModal.userId, selectedTenantIds);
            setPasswordResult(tenantAssignmentResult);
            setTenantAssignmentResult(null);
            tenantAssignmentModal.closeModal();
        } catch {
            /* parent shows error toast */
        } finally {
            setAssignmentSubmitting(false);
        }
    };

    const handleQuickFinish = async (values: CreateUserQuickFormValues) => {
        if (!quickMode) return;
        const tenantId = fixedTenantId ?? values.tenantId;
        if (!tenantId && !canDeferQuickTenantAssignment) {
            quickForm.setFields([{ name: 'tenantId', errors: [t('users.create.tenantRequired')] }]);
            return;
        }
        setQuickSubmitting(true);
        try {
            if (tenantId) {
                const result = await quickMode.onSubmit({
                    role: values.role,
                    tenantId,
                });
                if (result?.success) {
                    setQuickResult(result);
                }
                return;
            }
            if (quickMode.onSubmitWithoutTenant) {
                const result = await quickMode.onSubmitWithoutTenant({
                    role: values.role,
                });
                if (result?.success && result.generatedPassword) {
                    setTenantAssignmentResult(result);
                    tenantAssignmentModal.openModal({
                        userId: result.userId,
                        userEmail: result.email,
                        initialSelectedTenantIds: [],
                    });
                }
            }
        } catch {
            /* parent shows error toast */
        } finally {
            setQuickSubmitting(false);
        }
    };

    const copyPassword = async () => {
        if (!password) {
            message.error('Kein Passwort zum Kopieren vorhanden');
            return;
        }

        const copied = await copyTextToClipboard(password);
        if (copied) {
            message.success(t('tenants.provisioning.copySuccess'));
        } else {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    const closePasswordModal = () => {
        setPasswordResult(null);
        setTenantAssignmentResult(null);
        tenantAssignmentModal.closeModal();
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

    const loading = activeTab === 'quick' ? quickSubmitting : confirmLoading || submitting || assignmentSubmitting;

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

            {createUserTenantField}

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
                    rules={canDeferQuickTenantAssignment ? [] : [{ required: true, message: t('users.create.tenantRequired') }]}
                >
                    <TenantSelector
                        tenants={tenantRows}
                        loading={tenantsLoading}
                        placeholder={
                            canDeferQuickTenantAssignment
                                ? t('tenants.users.quick.tenantPlaceholderOptional')
                                : t('users.create.tenantPlaceholder')
                        }
                    />
                </Form.Item>
            ) : null}

            <Form.Item name="role" label={t('tenants.users.quick.role')} rules={[{ required: true }]}>
                <Select options={quickRoleOptions} />
            </Form.Item>

            <div
                style={{
                    marginTop: 16,
                    padding: 12,
                    background: '#fafafa',
                    borderRadius: 8,
                    border: '1px solid #f0f0f0',
                }}
            >
                <Typography.Paragraph style={{ marginBottom: 8 }}>
                    <Typography.Text strong>{t('tenants.users.quick.preview.usernameLabel')}</Typography.Text>{' '}
                    <Typography.Text code>
                        {usernameSuggestion?.suggestedUsername ?? quickUsernamePattern}
                    </Typography.Text>
                </Typography.Paragraph>
                {quickUsernameAlternates ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                        {t('tenants.users.quick.preview.usernameAvailable', { list: quickUsernameAlternates })}
                    </Typography.Paragraph>
                ) : null}
                <Typography.Paragraph style={{ marginBottom: 8 }}>
                    <Typography.Text strong>{t('tenants.users.quick.preview.emailLabel')}</Typography.Text>{' '}
                    {quickEmailPreview}
                </Typography.Paragraph>
                <Typography.Paragraph style={{ marginBottom: 0 }}>
                    <Typography.Text strong>{t('tenants.users.quick.preview.passwordLabel')}</Typography.Text>{' '}
                    {t('tenants.users.quick.autoPassword')}
                </Typography.Paragraph>
            </div>

            <Alert
                type="info"
                showIcon
                style={{ marginTop: 16 }}
                title={t('tenants.users.quick.autoTitle')}
                description={t('tenants.users.quick.autoForceChange')}
            />
            {showTenantPicker && canDeferQuickTenantAssignment && !watchedQuickTenantId ? (
                <Alert
                    type="warning"
                    showIcon
                    title={t('tenants.users.quick.assignmentRequiredAfterCreate')}
                    style={{ marginTop: 16 }}
                />
            ) : null}
        </Form>
    ) : null;

    return (
        <>
            <Modal
                title={modalTitle}
                open={open && !passwordResult && !quickResult && !tenantAssignmentModal.visible}
                onCancel={handleClose}
                width={600}
                destroyOnHidden
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

            {tenantAssignmentModal.visible && tenantAssignmentResult ? (
                <UserTenantAssignmentModal
                    open
                    userEmail={tenantAssignmentModal.userEmail}
                    currentTenants={tenantAssignmentModal.userTenants}
                    allTenants={tenantRows}
                    confirmLoading={assignmentSubmitting}
                    cancelText={t('common.buttons.close')}
                    onClose={() => {
                        setPasswordResult(tenantAssignmentResult);
                        setTenantAssignmentResult(null);
                        tenantAssignmentModal.closeModal();
                    }}
                    onSave={handleTenantAssignmentSave}
                    initialSelectedTenantIds={tenantAssignmentModal.initialSelectedTenantIds}
                />
            ) : null}

            <Modal
                title={t('users.create.success')}
                open={!!passwordResult}
                onCancel={closePasswordModal}
                destroyOnHidden
                footer={[
                    <Button key="copy" type="primary" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                        {t('users.create.copyPassword')}
                    </Button>,
                    <Button key="close" onClick={closePasswordModal}>
                        {t('users.create.close')}
                    </Button>,
                ]}
            >
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    {passwordResult?.userName ? (
                        <CredentialCopyRow
                            label={t('tenants.users.quick.result.usernameLabel')}
                            value={passwordResult.userName}
                        />
                    ) : null}
                    {passwordResult?.email ? (
                        <CredentialCopyRow
                            label={t('tenants.users.quick.result.emailLabel')}
                            value={passwordResult.email}
                        />
                    ) : null}
                    <CredentialCopyRow label={t('users.create.password')} value={password} />
                    <Alert
                        type="warning"
                        showIcon
                        title={t('users.create.passwordWarningTitle')}
                        description={t('users.create.generatedPasswordInfo')}
                    />
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
