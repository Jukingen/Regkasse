'use client';

import React from 'react';
import Link from 'next/link';
import { Alert, Button, Checkbox, Form, Input, Modal, Space, Typography } from 'antd';
import { StopOutlined } from '@ant-design/icons';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { canDecommissionRegister, rawRegisterStatus } from '@/features/cash-registers/utils/registerStatus';

export type DecommissionModalProps = {
    open: boolean;
    register: CashRegister | null;
    reason: string;
    onReasonChange: (value: string) => void;
    onCancel: () => void;
    onConfirm: (reason?: string) => void;
    confirmLoading?: boolean;
};

export function DecommissionModal(props: DecommissionModalProps) {
    if (!props.open) {
        return null;
    }
    return <DecommissionModalContent {...props} />;
}

function DecommissionModalContent({
    open,
    register,
    reason,
    onReasonChange,
    onCancel,
    onConfirm,
    confirmLoading,
}: DecommissionModalProps) {
    const { t } = useI18n();
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
    const [form] = Form.useForm<{ reason?: string; confirm?: boolean }>();
    const status = register ? rawRegisterStatus(register) : undefined;
    const canProceed = canDecommissionRegister(status);
    const name = register?.location?.trim() || '—';
    const number = register?.registerNumber?.trim() || '—';

    React.useEffect(() => {
        if (!open) {
            return;
        }

        form.setFieldsValue({
            reason,
            confirm: false,
        });
    }, [form, open, reason]);

    const handleCancel = () => {
        form.resetFields();
        onCancel();
    };

    const handleSubmit = (values: { reason?: string; confirm?: boolean }) => {
        const nextReason = values.reason?.trim() ?? '';
        onReasonChange(nextReason);
        onConfirm(nextReason);
    };

    return (
        <Modal
            title={
                <Space>
                    <StopOutlined style={{ color: '#ff4d4f' }} />
                    <span>{t('cashRegisters.decommission.modalTitleWithNumber', { number })}</span>
                </Space>
            }
            open={open}
            onCancel={handleCancel}
            footer={[
                <Button key="cancel" onClick={handleCancel}>
                    {t('cashRegisters.decommission.cancel')}
                </Button>,
                <Button
                    key="submit"
                    type="primary"
                    danger
                    loading={confirmLoading}
                    disabled={!canProceed}
                    onClick={() => form.submit()}
                >
                    {t('cashRegisters.decommission.confirm')}
                </Button>,
            ]}
            forceRender
            width={500}
        >
            <Typography.Paragraph strong style={{ marginBottom: 16 }}>
                {t('cashRegisters.decommission.registerLine', { name, number })}
            </Typography.Paragraph>

            {!canProceed ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={t('cashRegisters.decommission.mustCloseFirst')}
                />
            ) : null}

            <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 16 }}
                title={t('cashRegisters.decommission.irreversibleWarning')}
                description={
                    <ul style={{ margin: '8px 0 0 20px', padding: 0 }}>
                        <li>{t('cashRegisters.decommission.warningNoPayments')}</li>
                        <li>{t('cashRegisters.decommission.warningAutoReceipt')}</li>
                        <li>{t('cashRegisters.decommission.warningNoRestore')}</li>
                        <li>{t('cashRegisters.decommission.warningRetention')}</li>
                    </ul>
                }
            />
            {canOpenSonderbelege ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={
                        <span>
                            {t('cashRegisters.decommission.hintSchlussbeleg')}{' '}
                            <Link href="/rksv/sonderbelege?focus=schlussbeleg">
                                {t('cashRegisters.decommission.hintSchlussbelegLink')}
                            </Link>
                        </span>
                    }
                />
            ) : null}

            <Form
                form={form}
                layout="vertical"
                onFinish={handleSubmit}
                onValuesChange={(changedValues) => {
                    if (typeof changedValues.reason === 'string') {
                        onReasonChange(changedValues.reason);
                    }
                }}
            >
                <Form.Item
                    name="reason"
                    label={t('cashRegisters.decommission.reasonLabel')}
                    tooltip={t('cashRegisters.decommission.reasonTooltip')}
                >
                    <Input.TextArea
                        rows={3}
                        placeholder={t('cashRegisters.decommission.reasonPlaceholder')}
                        maxLength={450}
                        disabled={!canProceed}
                    />
                </Form.Item>

                <Form.Item
                    name="confirm"
                    valuePropName="checked"
                    rules={[
                        {
                            validator: async (_, value) => {
                                if (value) {
                                    return;
                                }
                                throw new Error(t('cashRegisters.decommission.confirmRequired'));
                            },
                        },
                    ]}
                >
                    <Checkbox disabled={!canProceed}>
                        {t('cashRegisters.decommission.confirmCheckbox')}
                    </Checkbox>
                </Form.Item>
            </Form>
        </Modal>
    );
}
