'use client';

import { Alert, Input, Modal, Typography } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';

export type CashRegisterHardDeleteModalProps = {
    open: boolean;
    register: CashRegister | null;
    confirmText: string;
    onConfirmTextChange: (value: string) => void;
    onCancel: () => void;
    onConfirm: () => void;
    confirmLoading?: boolean;
};

export function CashRegisterHardDeleteModal({
    open,
    register,
    confirmText,
    onConfirmTextChange,
    onCancel,
    onConfirm,
    confirmLoading,
}: CashRegisterHardDeleteModalProps) {
    const { t } = useI18n();
    const name = register?.location?.trim() || '—';
    const number = register?.registerNumber?.trim() || '—';
    const phraseOk = confirmText.trim() === 'HARD_DELETE';

    return (
        <Modal
            title={t('cashRegisters.hardDelete.modalTitle')}
            open={open}
            onCancel={onCancel}
            onOk={onConfirm}
            okText={t('cashRegisters.hardDelete.confirm')}
            cancelText={t('cashRegisters.decommission.cancel')}
            okButtonProps={{ danger: true, loading: confirmLoading, disabled: !phraseOk }}
            destroyOnHidden
            width={520}
        >
            <Alert
                type="error"
                showIcon
                title={t('cashRegisters.hardDelete.testOnlyBanner')}
                style={{ marginBottom: 16 }}
            />
            <Typography.Paragraph>
                {t('cashRegisters.hardDelete.registerLine', { name, number })}
            </Typography.Paragraph>
            <Typography.Paragraph type="secondary">{t('cashRegisters.hardDelete.body')}</Typography.Paragraph>
            <Typography.Text>{t('cashRegisters.hardDelete.confirmLabel')}</Typography.Text>
            <Input
                value={confirmText}
                onChange={(e) => onConfirmTextChange(e.target.value)}
                placeholder="HARD_DELETE"
                style={{ marginTop: 8 }}
                autoComplete="off"
            />
        </Modal>
    );
}
