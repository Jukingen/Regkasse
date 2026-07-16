'use client';

import { Button, Descriptions, Modal, Space, Tag, Typography } from 'antd';
import { CloudSyncOutlined, SafetyOutlined } from '@ant-design/icons';
import type { CashRegister } from '@/api/generated/model';
import { useI18n, formatCurrency, formatDateTime } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import { formatRelativeTime } from '@/features/cash-registers/utils/formatRelativeTime';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { CashRegisterStatusContextAlert } from '@/features/cash-registers/components/CashRegisterStatusContextAlert';
import { rawRegisterStatus, readDecommissionMeta, readStartbelegCreatedAt } from '@/features/cash-registers/utils/registerStatus';

export type RegisterDetailModalProps = {
    open: boolean;
    register: CashRegister | null;
    onClose: () => void;
    /** @deprecated Use CashRegisterStatusBadge. */
    statusLabel?: (status: number | undefined) => string;
    showHardDelete?: boolean;
    onHardDelete?: () => void;
};

export function RegisterDetailModal({
    open,
    register,
    onClose,
    showHardDelete,
    onHardDelete,
}: RegisterDetailModalProps) {
    const { t, formatLocale } = useI18n();
    const enhanced = register as EnhancedCashRegister | null;
    const status = register ? rawRegisterStatus(register) : undefined;
    const decommissionMeta = register ? readDecommissionMeta(register) : null;
    const registerNumber = register?.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY;
    const registerId = register?.id?.trim();
    const offlineHref = registerId
        ? `/admin/tse/offline-transactions?cashRegisterId=${encodeURIComponent(registerId)}`
        : '/admin/tse/offline-transactions';

    const cashierName =
        enhanced?.currentCashierName?.trim() ||
        register?.currentUser?.userName?.trim() ||
        register?.currentUserId?.trim() ||
        null;

    const startbelegCreatedAt = readStartbelegCreatedAt(register);

    const deviceModel = enhanced?.deviceInfo?.model?.trim();
    const deviceOs = enhanced?.deviceInfo?.osVersion?.trim();
    const deviceApp = enhanced?.deviceInfo?.appVersion?.trim();

    return (
        <Modal
            title={t('cashRegisters.detail.titleWithNumber', { number: registerNumber })}
            open={open}
            onCancel={onClose}
            footer={
                <Space wrap>
                    <Button onClick={onClose}>{t('cashRegisters.create.cancel')}</Button>
                    <Button icon={<SafetyOutlined />} href="/rksv/status">
                        {t('cashRegisters.detail.openTseDetails')}
                    </Button>
                    {(enhanced?.offlineQueueCount ?? 0) > 0 ? (
                        <Button icon={<CloudSyncOutlined />} href={offlineHref}>
                            {t('cashRegisters.actions.offlineQueue')}
                        </Button>
                    ) : null}
                    {showHardDelete && onHardDelete ? (
                        <Button danger onClick={onHardDelete}>
                            {t('cashRegisters.hardDelete.action')}
                        </Button>
                    ) : null}
                </Space>
            }
            width={640}
            destroyOnHidden
        >
            {register ? (
                <CashRegisterStatusContextAlert register={register} showOpenPrerequisites />
            ) : null}
            {register ? (
                <Descriptions bordered column={1} size="small">
                    <Descriptions.Item label={t('cashRegisters.detail.location')}>
                        {register.location?.trim() || FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.columns.status')}>
                        <CashRegisterStatusBadge register={register} />
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.tseStatus')}>
                        <TseHealthBadge
                            status={enhanced?.tseHealthStatus}
                            registerId={registerId}
                            offlineQueueCount={enhanced?.offlineQueueCount}
                        />
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.startbelegCreatedAt')}>
                        {startbelegCreatedAt
                            ? formatDateTime(startbelegCreatedAt, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastMonatsbelegUtc')}>
                        {enhanced?.lastMonatsbelegUtc
                            ? formatDateTime(enhanced.lastMonatsbelegUtc, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastJahresbelegUtc')}>
                        {enhanced?.lastJahresbelegUtc
                            ? formatDateTime(enhanced.lastJahresbelegUtc, formatLocale)
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.offlineQueueCount')}>
                        {typeof enhanced?.offlineQueueCount === 'number'
                            ? t('cashRegisters.detail.offlineQueueTransactions', {
                                  count: enhanced.offlineQueueCount,
                              })
                            : FORMAT_EMPTY_DISPLAY}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.lastSyncAtUtc')}>
                        {enhanced?.lastSyncAtUtc ? (
                            <Space orientation="vertical" size={0}>
                                <Typography.Text>
                                    {formatRelativeTime(enhanced.lastSyncAtUtc, formatLocale)}
                                </Typography.Text>
                                <Typography.Text type="secondary">
                                    {formatDateTime(enhanced.lastSyncAtUtc, formatLocale)}
                                </Typography.Text>
                            </Space>
                        ) : (
                            FORMAT_EMPTY_DISPLAY
                        )}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.currentCashier')}>
                        {cashierName ?? t('cashRegisters.detail.noCashier')}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.deviceInfoTitle')}>
                        {deviceModel || deviceOs || deviceApp ? (
                            <Space orientation="vertical" size={0}>
                                <Typography.Text>
                                    {deviceModel || t('cashRegisters.detail.deviceUnknown')} /{' '}
                                    {deviceOs || '—'}
                                </Typography.Text>
                                {deviceApp ? (
                                    <Typography.Text type="secondary">
                                        {t('cashRegisters.detail.deviceApp')}: {deviceApp}
                                    </Typography.Text>
                                ) : null}
                            </Space>
                        ) : (
                            FORMAT_EMPTY_DISPLAY
                        )}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('cashRegisters.detail.currentBalance')}>
                        {formatCurrency(register.currentBalance, formatLocale)}
                    </Descriptions.Item>
                    {decommissionMeta?.decommissionedAtUtc ? (
                        <Descriptions.Item label={t('cashRegisters.detail.decommissionedAt')}>
                            {formatDateTime(decommissionMeta.decommissionedAtUtc, formatLocale)}
                        </Descriptions.Item>
                    ) : null}
                </Descriptions>
            ) : null}
        </Modal>
    );
}
