'use client';

import React from 'react';
import { Alert, Checkbox, Form, Input, Modal, Typography } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { canDecommissionRegister, rawRegisterStatus } from '@/features/cash-registers/utils/registerStatus';

export type BulkDecommissionModalProps = {
    open: boolean;
    registers: CashRegister[];
    onCancel: () => void;
    onConfirm: (reason: string) => void;
    confirmLoading?: boolean;
};

export function BulkDecommissionModal({
    open,
    registers,
    onCancel,
    onConfirm,
    confirmLoading,
}: BulkDecommissionModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<{ reason?: string; confirm?: boolean }>();

    const eligible = registers.filter((r) => canDecommissionRegister(rawRegisterStatus(r)));
    const skipped = registers.length - eligible.length;

    React.useEffect(() => {
        if (!open) {
            return;
        }
        form.setFieldsValue({ reason: '', confirm: false });
    }, [form, open]);

    const handleCancel = () => {
        form.resetFields();
        onCancel();
    };

    const handleSubmit = (values: { reason?: string; confirm?: boolean }) => {
        if (!values.confirm) {
            return;
        }
        onConfirm(values.reason?.trim() || '');
    };

    return (
        <Modal
            title={t('cashRegisters.bulk.decommissionTitle')}
            open={open}
            onCancel={handleCancel}
            onOk={() => form.submit()}
            okText={t('cashRegisters.bulk.decommissionConfirm')}
            cancelText={t('cashRegisters.decommission.cancel')}
            okButtonProps={{ danger: true, loading: confirmLoading, disabled: eligible.length === 0 }}
            destroyOnHidden
        >
            <Alert
                type="warning"
                showIcon
                title={t('cashRegisters.decommission.irreversibleWarning')}
                style={{ marginBottom: 16 }}
            />
            <Typography.Paragraph>
                {t('cashRegisters.bulk.decommissionSummary', {
                    eligible: eligible.length,
                    total: registers.length,
                })}
            </Typography.Paragraph>
            {skipped > 0 ? (
                <Typography.Paragraph type="secondary">
                    {t('cashRegisters.bulk.skippedCount', { count: skipped })}
                </Typography.Paragraph>
            ) : null}
            <ul style={{ marginBottom: 16, paddingInlineStart: 20 }}>
                {eligible.map((r) => (
                    <li key={r.id ?? r.registerNumber}>
                        {r.registerNumber} — {r.location}
                    </li>
                ))}
            </ul>
            <Form form={form} layout="vertical" onFinish={handleSubmit}>
                <Form.Item
                    name="reason"
                    label={t('cashRegisters.decommission.reasonLabel')}
                    tooltip={t('cashRegisters.decommission.reasonTooltip')}
                >
                    <Input.TextArea
                        rows={3}
                        maxLength={450}
                        placeholder={t('cashRegisters.decommission.reasonPlaceholder')}
                    />
                </Form.Item>
                <Form.Item
                    name="confirm"
                    valuePropName="checked"
                    rules={[
                        {
                            validator: (_, value) =>
                                value
                                    ? Promise.resolve()
                                    : Promise.reject(new Error(t('cashRegisters.decommission.confirmRequired'))),
                        },
                    ]}
                >
                    <Checkbox>{t('cashRegisters.decommission.confirmCheckbox')}</Checkbox>
                </Form.Item>
            </Form>
        </Modal>
    );
}
