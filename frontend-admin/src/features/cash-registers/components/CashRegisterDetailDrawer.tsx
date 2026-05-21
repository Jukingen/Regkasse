'use client';

import { Button, Descriptions, Drawer, Space, Tag } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { useI18n, formatCurrency, formatDateTime } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import {
    rawRegisterStatus,
    readDecommissionMeta,
    registerStatusEmoji,
    registerStatusTagColor,
} from '@/features/cash-registers/utils/registerStatus';
export type CashRegisterDetailDrawerProps = {
    open: boolean;
    register: CashRegister | null;
    onClose: () => void;
    statusLabel: (status: number | undefined) => string;
    onHardDelete?: () => void;
    showHardDelete?: boolean;
};

export function CashRegisterDetailDrawer({
    open,
    register,
    onClose,
    statusLabel,
    onHardDelete,
    showHardDelete,
}: CashRegisterDetailDrawerProps) {
    const { t, formatLocale } = useI18n();
    const status = register ? rawRegisterStatus(register) : undefined;
    const decommissionMeta = register ? readDecommissionMeta(register) : null;

    return (
        <Drawer
            title={t('cashRegisters.detail.title')}
            open={open}
            onClose={onClose}
            width={480}
            destroyOnClose
        >
            {register ? (
                <Descriptions column={1} bordered size="small">
                    <Descriptions.Item label={t('cashRegisters.detail.location')}>
                        {register.location?.trim() || FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.registerNumber')}>
                        {register.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.columns.status')}>
                        <Tag color={registerStatusTagColor(status)}>
                            {registerStatusEmoji(status)} {statusLabel(status)}
                        </Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.currentBalance')}>
                        {formatCurrency(register.currentBalance, formatLocale)}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.startingBalance')}>
                        {formatCurrency(register.startingBalance, formatLocale)}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.currentUser')}>
                        {register.currentUser?.userName?.trim() ||
                            register.currentUserId?.trim() ||
                            FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastBalanceUpdate')}>
                        {register.lastBalanceUpdate
                            ? formatDateTime(register.lastBalanceUpdate, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.id')}>
                        {register.id?.trim() || FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    {decommissionMeta?.decommissionedAtUtc ? (
                        <Descriptions.Item label={t('cashRegisters.detail.decommissionedAt')}>
                            {formatDateTime(decommissionMeta.decommissionedAtUtc, formatLocale)}
                        </Descriptions.Item>
                    ) : null}
                    {decommissionMeta?.decommissionReason ? (
                        <Descriptions.Item label={t('cashRegisters.detail.decommissionReason')}>
                            {decommissionMeta.decommissionReason}
                        </Descriptions.Item>
                    ) : null}
                </Descriptions>
            ) : null}
            {showHardDelete && onHardDelete ? (
                <Space style={{ marginTop: 16 }}>
                    <Button danger onClick={onHardDelete}>
                        {t('cashRegisters.hardDelete.action')}
                    </Button>
                </Space>
            ) : null}
        </Drawer>
    );
}
