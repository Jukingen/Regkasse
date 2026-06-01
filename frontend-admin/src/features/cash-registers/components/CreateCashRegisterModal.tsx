'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useEffect, useMemo } from 'react';
import { Modal, Form, Input, Select } from 'antd';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useCreateCashRegister } from '@/features/cash-registers/hooks/useCreateCashRegister';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

type CreateCashRegisterFormValues = {
    registerNumber: string;
    location: string;
    tenantId?: string;
};

export type CreateCashRegisterModalProps = {
    visible: boolean;
    /** Manager: resolved mandant from useCurrentTenant (not sent in API). SuperAdmin: locks picker when set. */
    tenantId?: string;
    onClose: () => void;
    onSuccess?: () => void;
};

export function CreateCashRegisterModal({
    visible,
    tenantId: fixedTenantId,
    onClose,
    onSuccess,
}: CreateCashRegisterModalProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const { user } = useAuth();
    const isSuperAdminUser = isSuperAdmin(user?.role);
    const createMutation = useCreateCashRegister({
        onSuccess: () => {
            form.resetFields();
            onSuccess?.();
            onClose();
        },
    });
    const { tenants, isLoading: tenantsLoading } = useTenantList({ enabled: visible && isSuperAdminUser });

    const [form] = Form.useForm<CreateCashRegisterFormValues>();

    const showTenantPicker = isSuperAdminUser && !fixedTenantId;

    const tenantOptions = useMemo(
        () =>
            tenants.map((row) => ({
                value: row.id,
                label: t('cashRegisters.create.tenantOption', { name: row.name, slug: row.slug }),
            })),
        [tenants, t],
    );

    useEffect(() => {
        if (!visible) {
            form.resetFields();
            return;
        }
        if (fixedTenantId) {
            form.setFieldsValue({ tenantId: fixedTenantId });
        }
    }, [visible, fixedTenantId, form]);

    const handleClose = () => {
        form.resetFields();
        onClose();
    };

    const handleSubmit = async (values: CreateCashRegisterFormValues) => {
        try {
            await createMutation.mutateAsync({
                registerNumber: values.registerNumber.trim(),
                location: values.location.trim(),
                tenantId: isSuperAdminUser
                    ? (values.tenantId ?? fixedTenantId ?? undefined)
                    : undefined,
            });
        } catch (err) {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'CreateCashRegisterModal.submit',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        }
    };

    return (
        <Modal
            title={t('cashRegisters.create.modalTitle')}
            open={visible}
            onCancel={handleClose}
            onOk={() => form.submit()}
            okText={t('cashRegisters.create.confirm')}
            cancelText={t('cashRegisters.create.cancel')}
            confirmLoading={createMutation.isPending}
            destroyOnHidden
            width={480}
        >
            <Form form={form} layout="vertical" onFinish={handleSubmit} requiredMark="optional">
                <Form.Item
                    name="registerNumber"
                    label={t('cashRegisters.create.registerNumberLabel')}
                    rules={[
                        { required: true, message: t('cashRegisters.create.registerNumberRequired') },
                        { max: 20, message: t('cashRegisters.create.registerNumberMax') },
                    ]}
                >
                    <Input placeholder={t('cashRegisters.create.registerNumberPlaceholder')} />
                </Form.Item>

                <Form.Item
                    name="location"
                    label={t('cashRegisters.create.locationLabel')}
                    rules={[
                        { required: true, message: t('cashRegisters.create.locationRequired') },
                        { max: 100, message: t('cashRegisters.create.locationMax') },
                    ]}
                >
                    <Input placeholder={t('cashRegisters.create.locationPlaceholder')} />
                </Form.Item>

                {showTenantPicker ? (
                    <Form.Item
                        name="tenantId"
                        label={t('cashRegisters.create.tenantLabel')}
                        rules={[
                            { required: true, message: t('cashRegisters.create.tenantRequired') },
                        ]}
                    >
                        <Select
                            placeholder={t('cashRegisters.create.tenantPlaceholder')}
                            options={tenantOptions}
                            showSearch
                            optionFilterProp="label"
                            loading={tenantsLoading}
                        />
                    </Form.Item>
                ) : null}
            </Form>
        </Modal>
    );
}
