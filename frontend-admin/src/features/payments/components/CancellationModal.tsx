'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useCallback, useEffect, useState } from 'react';
import { Modal, Alert, Form, Input, Select, Space, Tag, Typography } from 'antd';
import { ClockCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import type { AdminPaymentDetailDto } from '@/api/generated/model';
import { cancelAdminPayment, type AdminCancelPaymentRequest } from '@/features/payments/api/adminPaymentCancel';
import {
    CANCELLATION_REASON_CODES,
    CANCELLATION_REASON_RISK,
    type CancellationReasonCode,
    type CancellationReasonRisk,
} from '@/features/payments/types/cancellationReasons';
import { useI18n } from '@/i18n';
import { formatCurrency, formatDateTime, FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { extractRawApiErrorMessage } from '@/shared/errors/extractRawApiErrorMessage';

type FormValues = {
    reasonCode: CancellationReasonCode;
    reasonText: string;
};

export interface CancellationModalProps {
    payment: AdminPaymentDetailDto;
    open: boolean;
    onClose: () => void;
    onSuccess: () => void;
    disabled?: boolean;
}

function riskTagColor(risk: CancellationReasonRisk): string {
    switch (risk) {
        case 'low':
            return 'green';
        case 'medium':
            return 'orange';
        case 'high':
            return 'red';
        default:
            return 'default';
    }
}

export function CancellationModal({
    payment,
    open,
    onClose,
    onSuccess,
    disabled = false,
}: CancellationModalProps) {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const [form] = Form.useForm<FormValues>();
    const [step, setStep] = useState<'form' | 'approval'>('form');
    const [waitTimeSeconds, setWaitTimeSeconds] = useState(0);
    const [approvalMessage, setApprovalMessage] = useState('');
    const [approvalReasons, setApprovalReasons] = useState<string[]>([]);
    const [approvalToken, setApprovalToken] = useState('');
    const [pendingRequest, setPendingRequest] = useState<AdminCancelPaymentRequest | null>(null);

    const paymentId = payment.id ?? '';

    const resetState = useCallback(() => {
        form.resetFields();
        setStep('form');
        setWaitTimeSeconds(0);
        setApprovalMessage('');
        setApprovalReasons([]);
        setApprovalToken('');
        setPendingRequest(null);
    }, [form]);

    useEffect(() => {
        if (!open) {
            resetState();
        }
    }, [open, resetState]);

    useEffect(() => {
        if (!open || step !== 'approval' || waitTimeSeconds <= 0) {
            return;
        }
        const timer = window.setInterval(() => {
            setWaitTimeSeconds((prev) => Math.max(0, prev - 1));
        }, 1000);
        return () => window.clearInterval(timer);
    }, [open, step, waitTimeSeconds]);

    const cancelMutation = useMutation({
        mutationFn: (payload: AdminCancelPaymentRequest) => cancelAdminPayment(paymentId, payload),
    });

    const executeCancel = async (payload: AdminCancelPaymentRequest) => {
        const response = await cancelMutation.mutateAsync(payload);

        if (response.requiresApproval && !response.success) {
            setPendingRequest(payload);
            setApprovalMessage(response.message ?? t('payments.cancellationModal.approvalRequiredFallback'));
            setApprovalReasons(response.reasons ?? []);
            setWaitTimeSeconds(response.waitTimeSeconds ?? 900);
            setStep('approval');
            setApprovalToken('');
            return;
        }

        if (!response.success) {
            throw new Error(response.message ?? t('payments.messages.cancelError'));
        }

        message.success(t('payments.messages.cancelSuccess'));
        onSuccess();
        onClose();
    };

    const handleSubmitForm = async () => {
        if (!paymentId) {
            message.error(t('payments.messages.errorNoPaymentSelected'));
            return;
        }
        try {
            const values = await form.validateFields();
            await executeCancel({
                reason: values.reasonText.trim(),
                reasonCode: values.reasonCode,
            });
        } catch (error) {
            if (error && typeof error === 'object' && 'errorFields' in error) {
                return;
            }
            message.error(extractRawApiErrorMessage(error) ?? t('payments.messages.cancelError'));
        }
    };

    const handleSubmitApproval = async () => {
        if (!pendingRequest) {
            return;
        }
        if (approvalToken.length !== 6) {
            message.error(t('payments.cancellationModal.tokenInvalidLength'));
            return;
        }
        if (waitTimeSeconds <= 0) {
            message.error(t('payments.cancellationModal.tokenExpired'));
            return;
        }
        try {
            await executeCancel({
                ...pendingRequest,
                approvalToken,
            });
        } catch (error) {
            message.error(extractRawApiErrorMessage(error) ?? t('payments.messages.cancelError'));
        }
    };

    const waitMinutes = Math.floor(waitTimeSeconds / 60);
    const waitSecondsRemainder = waitTimeSeconds % 60;

    const amountLabel = formatCurrency(payment.totalAmount ?? 0, formatLocale, {
        currency: payment.currency || 'EUR',
    });
    const dateLabel = payment.createdAt
        ? formatDateTime(payment.createdAt, formatLocale)
        : FORMAT_EMPTY_DISPLAY;

    return (
        <Modal
            title={t('payments.cancellationModal.title')}
            open={open}
            onCancel={onClose}
            onOk={step === 'form' ? handleSubmitForm : handleSubmitApproval}
            okText={
                step === 'form'
                    ? t('payments.cancellationModal.submit')
                    : t('payments.cancellationModal.confirmToken')
            }
            cancelText={t('payments.detail.cancelCancel')}
            confirmLoading={cancelMutation.isPending}
            okButtonProps={{
                danger: step === 'form',
                disabled: disabled || (step === 'approval' && waitTimeSeconds <= 0),
            }}
            destroyOnHidden
            width={520}
        >
            <Alert
                type="warning"
                showIcon
                icon={<WarningOutlined />}
                title={t('payments.cancellationModal.infoTitle')}
                description={t('payments.cancellationModal.infoDescription')}
                style={{ marginBottom: 16 }}
            />

            {step === 'form' ? (
                <Form form={form} layout="vertical" disabled={disabled}>
                    <Form.Item
                        name="reasonCode"
                        label={t('payments.cancellationModal.reasonCodeLabel')}
                        rules={[
                            {
                                required: true,
                                message: t('payments.cancellationModal.reasonCodeRequired'),
                            },
                        ]}
                    >
                        <Select
                            placeholder={t('payments.cancellationModal.reasonCodePlaceholder')}
                            optionLabelProp="label"
                        >
                            {CANCELLATION_REASON_CODES.map((code) => {
                                const risk = CANCELLATION_REASON_RISK[code];
                                const label = t(`payments.cancellationModal.reasons.${code}.label`);
                                return (
                                    <Select.Option key={code} value={code} label={label}>
                                        <Space>
                                            <span>{label}</span>
                                            <Tag color={riskTagColor(risk)}>
                                                {t(`payments.cancellationModal.risk.${risk}`)}
                                            </Tag>
                                        </Space>
                                    </Select.Option>
                                );
                            })}
                        </Select>
                    </Form.Item>

                    <Form.Item
                        name="reasonText"
                        label={t('payments.cancellationModal.reasonTextLabel')}
                        rules={[
                            {
                                required: true,
                                message: t('payments.cancellationModal.reasonTextRequired'),
                            },
                            {
                                min: 5,
                                message: t('payments.cancellationModal.reasonTextMinLength'),
                            },
                        ]}
                    >
                        <Input.TextArea
                            rows={4}
                            maxLength={500}
                            showCount
                            placeholder={t('payments.cancellationModal.reasonTextPlaceholder')}
                        />
                    </Form.Item>

                    <div
                        style={{
                            background: 'var(--ant-color-fill-quaternary, #f5f5f5)',
                            padding: 12,
                            borderRadius: 8,
                        }}
                    >
                        <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                            <div>
                                <strong>{t('payments.cancellationModal.summaryAmount')}:</strong> {amountLabel}
                            </div>
                            <div>
                                <strong>{t('payments.cancellationModal.summaryDate')}:</strong> {dateLabel}
                            </div>
                            <div>
                                <strong>{t('payments.cancellationModal.summaryReceipt')}:</strong>{' '}
                                {payment.receiptNumber ?? FORMAT_EMPTY_DISPLAY}
                            </div>
                        </Typography.Text>
                    </div>
                </Form>
            ) : (
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    <Alert
                        type="info"
                        showIcon
                        title={t('payments.cancellationModal.approvalTitle')}
                        description={
                            <>
                                <Typography.Paragraph style={{ marginBottom: 8 }}>
                                    {approvalMessage}
                                </Typography.Paragraph>
                                {approvalReasons.length > 0 ? (
                                    <ul style={{ margin: 0, paddingLeft: 20 }}>
                                        {approvalReasons.map((reason) => (
                                            <li key={reason}>{reason}</li>
                                        ))}
                                    </ul>
                                ) : null}
                                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                                    {t('payments.cancellationModal.approvalEmailHint')}
                                </Typography.Paragraph>
                            </>
                        }
                    />

                    <Alert
                        type={waitTimeSeconds > 0 ? 'warning' : 'error'}
                        showIcon
                        icon={<ClockCircleOutlined />}
                        title={
                            waitTimeSeconds > 0
                                ? t('payments.cancellationModal.tokenValidFor', {
                                      minutes: waitMinutes,
                                      seconds: waitSecondsRemainder,
                                  })
                                : t('payments.cancellationModal.tokenExpired')
                        }
                    />

                    <Form layout="vertical">
                        <Form.Item
                            label={t('payments.cancellationModal.tokenLabel')}
                            required
                            extra={t('payments.cancellationModal.tokenExtra')}
                        >
                            <Input.OTP
                                length={6}
                                value={approvalToken}
                                onChange={(value) =>
                                    setApprovalToken(value.replace(/\D/g, '').slice(0, 6))
                                }
                                disabled={cancelMutation.isPending || waitTimeSeconds <= 0}
                            />
                        </Form.Item>
                    </Form>
                </Space>
            )}
        </Modal>
    );
}
