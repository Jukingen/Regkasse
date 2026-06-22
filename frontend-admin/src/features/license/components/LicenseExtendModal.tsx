'use client';

import { useEffect, useMemo, useState } from 'react';
import { Button, Descriptions, Flex, Form, Input, Modal, Tag, Typography } from 'antd';

import { useI18n, formatDate } from '@/i18n';
import { LicensePreviewDetails } from '@/features/license/components/LicensePreviewDetails';
import type { TenantLicenseStatus } from '@/features/license/api/tenantLicense';
import type { ExtendTenantLicenseResult } from '@/features/license/api/tenantLicense';
import type { TenantLicensePreviewResult } from '@/features/license/api/tenantLicense';
import {
    useExtendTenantLicense,
    type ExtendTenantLicenseFormValues,
} from '@/features/license/hooks/useExtendTenantLicense';
import { useLicensePreview } from '@/features/license/hooks/useLicensePreview';
import {
    getLicenseStatusLabel,
    getLicenseStatusTagColor,
    type ResolvedLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import {
    isLicenseExtendConfirmEnabled,
    isLicenseExtendPreviewVisible,
    isLicenseKeyInputDisabled,
    isLicensePreviewButtonDisabled,
    resolveLicenseExtendUiState,
} from '@/features/license/utils/licenseExtendModalState';

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
    const [preview, setPreview] = useState<TenantLicensePreviewResult | null>(null);
    const [extendResult, setExtendResult] = useState<ExtendTenantLicenseResult | null>(null);
    const previewMutation = useLicensePreview();
    const extendMutation = useExtendTenantLicense(tenantId);

    const uiState = useMemo(
        () =>
            resolveLicenseExtendUiState({
                preview,
                extendResult,
                isPreviewLoading: previewMutation.isPending,
                isExtendPending: extendMutation.isPending,
            }),
        [preview, extendResult, previewMutation.isPending, extendMutation.isPending],
    );

    useEffect(() => {
        if (open) {
            form.setFieldsValue({ licenseKey: '' });
            setPreview(null);
            setExtendResult(null);
        }
    }, [open, form]);

    const handleClose = () => {
        if (!extendMutation.isPending && !previewMutation.isPending) {
            form.resetFields();
            setPreview(null);
            setExtendResult(null);
            onClose();
        }
    };

    const handlePreview = async () => {
        const values = await form.validateFields(['licenseKey']);
        const result = await previewMutation.mutateAsync({ licenseKey: values.licenseKey });
        setPreview(result);
    };

    const handleExtend = async () => {
        const licenseKey = form.getFieldValue('licenseKey')?.trim();
        if (!licenseKey || !isLicenseExtendConfirmEnabled(uiState)) return;
        const result = await extendMutation.mutateAsync({ licenseKey });
        setExtendResult(result);
        onSuccess?.();
    };

    const inputDisabled = isLicenseKeyInputDisabled(uiState);
    const previewButtonDisabled = isLicensePreviewButtonDisabled(uiState);
    const confirmEnabled = isLicenseExtendConfirmEnabled(uiState);
    const showPreviewArea = isLicenseExtendPreviewVisible(uiState);

    return (
        <Modal
            title={t('license.extendModal.title')}
            open={open}
            onCancel={handleClose}
            footer={null}
            destroyOnHidden
        >
            {status ? (
                <Descriptions
                    size="small"
                    column={1}
                    style={{ marginBottom: 16 }}
                    title={t('license.extendModal.currentStatus')}
                >
                    <Descriptions.Item label={t('license.extendModal.statusLabel')}>
                        <Tag color={getLicenseStatusTagColor(resolvedStatus?.kind ?? 'no_license')}>
                            {getLicenseStatusLabel(resolvedStatus?.kind ?? 'no_license', t)}
                        </Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('license.extendModal.validUntilLabel')}>
                        {status.validUntilUtc ? formatDate(status.validUntilUtc, formatLocale) : '—'}
                    </Descriptions.Item>
                </Descriptions>
            ) : null}

            <Form
                form={form}
                layout="vertical"
                onValuesChange={() => {
                    if (uiState !== 'success' && uiState !== 'confirming') {
                        setPreview(null);
                        setExtendResult(null);
                    }
                }}
            >
                <Form.Item
                    label={t('license.extendModal.licenseKeyLabel')}
                    required
                    style={{ marginBottom: 8 }}
                >
                    <Flex gap={8} align="start">
                        <Form.Item
                            name="licenseKey"
                            style={{ flex: 1, marginBottom: 0 }}
                            rules={[{ required: true, message: t('license.extendModal.noLicenseKey') }]}
                        >
                            <Input
                                placeholder={t('license.extendModal.licenseKeyPlaceholder')}
                                autoComplete="off"
                                disabled={inputDisabled}
                                onPressEnter={(event) => {
                                    event.preventDefault();
                                    if (!previewButtonDisabled) {
                                        void handlePreview();
                                    }
                                }}
                            />
                        </Form.Item>
                        <Button
                            loading={uiState === 'loading'}
                            disabled={previewButtonDisabled}
                            onClick={() => void handlePreview()}
                        >
                            {t('license.extendModal.previewButton')}
                        </Button>
                    </Flex>
                </Form.Item>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 0 }}>
                    {t('license.extendModal.infoText')}
                </Typography.Text>

                {showPreviewArea ? (
                    <LicensePreviewDetails
                        uiState={uiState}
                        preview={preview}
                        extendResult={extendResult}
                        t={t}
                        formatLocale={formatLocale}
                    />
                ) : null}
            </Form>

            <Flex justify="space-between" style={{ marginTop: 24 }}>
                <Button onClick={handleClose} disabled={uiState === 'confirming'}>
                    {uiState === 'success' ? t('common.buttons.close') : t('common.buttons.cancel')}
                </Button>
                {uiState !== 'success' ? (
                    <Button
                        type="primary"
                        disabled={!confirmEnabled}
                        loading={uiState === 'confirming'}
                        onClick={() => void handleExtend()}
                    >
                        {t('license.extendModal.confirmButton')}
                    </Button>
                ) : null}
            </Flex>
        </Modal>
    );
}
