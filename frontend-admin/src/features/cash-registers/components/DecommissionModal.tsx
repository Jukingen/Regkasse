'use client';

import Link from 'next/link';
import { Alert, Input, Modal, Typography } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { canDecommissionRegister, rawRegisterStatus } from '@/features/cash-registers/utils/registerStatus';

export type DecommissionModalProps = {
    open: boolean;
    register: CashRegister | null;
    reason: string;
    onReasonChange: (value: string) => void;
    onCancel: () => void;
    onConfirm: () => void;
    confirmLoading?: boolean;
};

export function DecommissionModal({
    open,
    register,
    reason,
    onReasonChange,
    onCancel,
    onConfirm,
    confirmLoading,
}: DecommissionModalProps) {
    const { t } = useI18n();
    const status = register ? rawRegisterStatus(register) : undefined;
    const canProceed = canDecommissionRegister(status);
    const name = register?.location?.trim() || '—';
    const number = register?.registerNumber?.trim() || '—';

    return (
        <Modal
            title={t('cashRegisters.decommission.modalTitle')}
            open={open}
            onCancel={onCancel}
            onOk={onConfirm}
            okText={t('cashRegisters.decommission.confirm')}
            cancelText={t('cashRegisters.decommission.cancel')}
            okButtonProps={{ danger: true, loading: confirmLoading, disabled: !canProceed }}
            destroyOnClose
            width={560}
        >
            <Typography.Paragraph strong style={{ marginBottom: 16 }}>
                {t('cashRegisters.decommission.registerLine', { name, number })}
            </Typography.Paragraph>

            {!canProceed ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={t('cashRegisters.decommission.mustCloseFirst')}
                />
            ) : null}

            <Typography.Text strong>{t('cashRegisters.decommission.warningTitle')}</Typography.Text>
            <ul style={{ marginTop: 8, marginBottom: 16, paddingLeft: 20 }}>
                <li>{t('cashRegisters.decommission.warningNoPayments')}</li>
                <li>{t('cashRegisters.decommission.warningRetention')}</li>
                <li>{t('cashRegisters.decommission.warningNoRestore')}</li>
            </ul>

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message={t('cashRegisters.decommission.schlussbelegFlow')}
            />
            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message={
                    <span>
                        {t('cashRegisters.decommission.hintSchlussbeleg')}{' '}
                        <Link href="/rksv/sonderbelege?focus=schlussbeleg">
                            {t('cashRegisters.decommission.hintSchlussbelegLink')}
                        </Link>
                    </span>
                }
            />

            <Typography.Text>{t('cashRegisters.decommission.reasonLabel')}</Typography.Text>
            <Input.TextArea
                rows={3}
                value={reason}
                onChange={(e) => onReasonChange(e.target.value)}
                placeholder={t('cashRegisters.decommission.reasonPlaceholder')}
                maxLength={450}
                style={{ marginTop: 8 }}
                disabled={!canProceed}
            />
        </Modal>
    );
}
