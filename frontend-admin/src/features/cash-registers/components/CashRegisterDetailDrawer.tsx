'use client';

import { Button, Descriptions, Divider, Drawer, Space, Tag, Typography } from 'antd';
import { CloudSyncOutlined, SafetyOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import type { CashRegister } from '@/api/generated/model';
import { useI18n, formatCurrency, formatDateTime } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { getCashRegisterTseHealth } from '@/features/cash-registers/api/cashRegisters';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
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
    const registerNumber = register?.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY;
    const enhanced = register as EnhancedCashRegister | null;
    const registerId = register?.id?.trim();

    const tseHealthQuery = useQuery({
        queryKey: ['admin', 'cash-registers', registerId, 'tse-health'],
        queryFn: () => getCashRegisterTseHealth(registerId!),
        enabled: open && Boolean(registerId),
        staleTime: 15_000,
    });

    const cashierName =
        enhanced?.currentCashierName?.trim() ||
        register?.currentUser?.userName?.trim() ||
        register?.currentUserId?.trim() ||
        null;

    const offlineHref = registerId
        ? `/admin/tse/offline-transactions?cashRegisterId=${encodeURIComponent(registerId)}`
        : '/admin/tse/offline-transactions';

    return (
        <Drawer
            title={t('cashRegisters.detail.titleWithNumber', { number: registerNumber })}
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
                    <Descriptions.Item label={t('cashRegisters.detail.createdAt')}>
                        {register.createdAt
                            ? formatDateTime(register.createdAt, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.currentBalance')}>
                        {formatCurrency(register.currentBalance, formatLocale)}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.startingBalance')}>
                        {formatCurrency(register.startingBalance, formatLocale)}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.currentCashier')}>
                        {cashierName ?? FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastSyncAtUtc')}>
                        {enhanced?.lastSyncAtUtc
                            ? formatDateTime(enhanced.lastSyncAtUtc, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.offlineQueueCount')}>
                        {typeof enhanced?.offlineQueueCount === 'number'
                            ? enhanced.offlineQueueCount
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastBalanceUpdate')}>
                        {register.lastBalanceUpdate
                            ? formatDateTime(register.lastBalanceUpdate, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.startbelegCreatedAt')}>
                        {register.startbelegCreatedAt
                            ? formatDateTime(register.startbelegCreatedAt, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastMonatsbelegUtc')}>
                        {register.lastMonatsbelegUtc
                            ? formatDateTime(register.lastMonatsbelegUtc, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastJahresbelegUtc')}>
                        {register.lastJahresbelegUtc
                            ? formatDateTime(register.lastJahresbelegUtc, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.id')}>
                        {register.id?.trim() || FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.tseStatus')}>
                        <Space direction="vertical" size={4}>
                            <TseHealthBadge
                                status={
                                    tseHealthQuery.data?.status ?? enhanced?.tseHealthStatus
                                }
                            />
                            {tseHealthQuery.data?.message ? (
                                <Typography.Text type="secondary">
                                    {tseHealthQuery.data.message}
                                </Typography.Text>
                            ) : null}
                            <Space wrap>
                                <Button
                                    icon={<SafetyOutlined />}
                                    size="small"
                                    href="/rksv/status"
                                >
                                    {t('cashRegisters.detail.openTseDetails')}
                                </Button>
                                {(enhanced?.offlineQueueCount ?? 0) > 0 ? (
                                    <Button
                                        icon={<CloudSyncOutlined />}
                                        size="small"
                                        href={offlineHref}
                                    >
                                        {t('cashRegisters.actions.offlineQueue')}
                                    </Button>
                                ) : null}
                            </Space>
                        </Space>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.deviceInfoTitle')}>
                        {enhanced?.deviceInfo?.model ||
                        enhanced?.deviceInfo?.osVersion ||
                        enhanced?.deviceInfo?.appVersion ? (
                            <Space direction="vertical" size={0}>
                                {enhanced.deviceInfo?.model ? (
                                    <Typography.Text>
                                        {t('cashRegisters.detail.deviceModel')}:{' '}
                                        {enhanced.deviceInfo.model}
                                    </Typography.Text>
                                ) : null}
                                {enhanced.deviceInfo?.osVersion ? (
                                    <Typography.Text>
                                        {t('cashRegisters.detail.deviceOs')}:{' '}
                                        {enhanced.deviceInfo.osVersion}
                                    </Typography.Text>
                                ) : null}
                                {enhanced.deviceInfo?.appVersion ? (
                                    <Typography.Text>
                                        {t('cashRegisters.detail.deviceApp')}:{' '}
                                        {enhanced.deviceInfo.appVersion}
                                    </Typography.Text>
                                ) : null}
                            </Space>
                        ) : (
                            FORMAT_EMPTY_DISPLAY
                        )}
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
            {register ? (
                <>
                    <Divider />
                    <Typography.Title level={5}>{t('cashRegisters.detail.specialReceiptsTitle')}</Typography.Title>
                    <Space wrap>
                        <Button size="small" href="/rksv/sonderbelege?focus=startbeleg">
                            {t('receipts.specialKind.startbeleg')}
                        </Button>
                        <Button size="small" href="/rksv/sonderbelege?focus=monatsbeleg">
                            {t('receipts.specialKind.monatsbeleg')}
                        </Button>
                        <Button size="small" href="/rksv/sonderbelege?focus=jahresbeleg">
                            {t('receipts.specialKind.jahresbeleg')}
                        </Button>
                        <Button size="small" danger href="/rksv/sonderbelege?focus=schlussbeleg">
                            {t('receipts.specialKind.schlussbeleg')}
                        </Button>
                    </Space>
                </>
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
