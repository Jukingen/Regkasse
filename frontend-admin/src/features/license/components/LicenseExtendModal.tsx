'use client';

import { useEffect } from 'react';
import { Button, Descriptions, Form, Input, Modal, Select, Space, Tag } from 'antd';

import { useI18n, formatDate } from '@/i18n';
import type { TenantLicenseStatus } from '@/features/license/api/tenantLicense';
import {
    useExtendTenantLicense,
    type ExtendTenantLicenseFormValues,
} from '@/features/license/hooks/useExtendTenantLicense';
import {
    getLicenseStatusLabel,
    getLicenseStatusTagColor,
    type ResolvedLicenseStatus,
} from '@/features/license/utils/licenseStatus';

const EXTEND_DURATION_OPTIONS = [
    { value: 30, labelKey: 'license.extendModal.duration30' },
    { value: 90, labelKey: 'license.extendModal.duration90' },
    { value: 365, labelKey: 'license.extendModal.duration365' },
] as const;

export type LicenseExtendModalProps = {
    open: boolean;
    tenantId: string;
    status: TenantLicenseStatus | null | undefined;
    resolvedStatus: ResolvedLicenseStatus | null;
    onClose: () => void;
    onSuccess?: () => void;
};

export function LicenseExtendModal({
    open,
    tenantId,
    status,
    resolvedStatus,
    onClose,
    onSuccess,
}: LicenseExtendModalProps) {
    const { t, formatLocale } = useI18n();
    const [form] = Form.useForm<ExtendTenantLicenseFormValues>();
    const extendMutation = useExtendTenantLicense(tenantId, status?.validUntilUtc);

    useEffect(() => {
        if (open) {
            form.setFieldsValue({ licenseKey: '', extendDays: 365 });
        }
    }, [open, form]);

    const handleClose = () => {
        if (!extendMutation.isPending) {
            form.resetFields();
            onClose();
        }
    };

    return (
        <Modal
            title={t('license.extendModal.title')}
            open={open}
            onCancel={handleClose}
            footer={null}
            destroyOnHidden
        >
            {status ? (
                <Descriptions size="small" column={1} style={{ marginBottom: 16 }} title={t('license.extendModal.currentStatus')}>
                    <Descriptions.Item label={t('license.tenant.status')}>
                        <Tag color={getLicenseStatusTagColor(resolvedStatus?.kind ?? 'no_license')}>
                            {getLicenseStatusLabel(resolvedStatus?.kind ?? 'no_license', t)}
                        </Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('license.tenant.validUntil')}>
                        {status.validUntilUtc ? formatDate(status.validUntilUtc, formatLocale) : '—'}
                    </Descriptions.Item>
                </Descriptions>
            ) : null}
            <Form
                form={form}
                layout="vertical"
                onFinish={async (values) => {
                    await extendMutation.mutateAsync(values);
                    form.resetFields();
                    onSuccess?.();
                    onClose();
                }}
            >
                <Form.Item
                    name="licenseKey"
                    label={t('license.extendModal.licenseKeyLabel')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                >
                    <Input placeholder="REGK-XXXXX-XXXXX-XXXXX" autoComplete="off" />
                </Form.Item>
                <Form.Item
                    name="extendDays"
                    label={t('license.extendModal.durationLabel')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                >
                    <Select
                        options={EXTEND_DURATION_OPTIONS.map((option) => ({
                            value: option.value,
                            label: t(option.labelKey),
                        }))}
                    />
                </Form.Item>
                <Space>
                    <Button onClick={handleClose}>{t('common.buttons.cancel')}</Button>
                    <Button type="primary" htmlType="submit" loading={extendMutation.isPending}>
                        {t('license.extendModal.confirmButton')}
                    </Button>
                </Space>
            </Form>
        </Modal>
    );
}
