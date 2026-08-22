'use client';

import { CloudSyncOutlined, SafetyOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Descriptions, Divider, Drawer, Space, Tooltip, Typography } from 'antd';

import type { CashRegister } from '@/api/generated/model';
import { getCashRegisterTseHealth } from '@/features/cash-registers/api/cashRegisters';
import { CashRegisterAssignedUserField } from '@/features/cash-registers/components/CashRegisterAssignedUserField';
import { CashierDisplay } from '@/features/cash-registers/components/CashierDisplay';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { CashRegisterStatusContextAlert } from '@/features/cash-registers/components/CashRegisterStatusContextAlert';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import { LimitWarning } from '@/features/tenants/components/LimitWarning';
import { useCashRegisterPermissions } from '@/features/cash-registers/hooks/useCashRegisterPermissions';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import {
  isDecommissionedRegister,
  rawRegisterStatus,
  readDecommissionMeta,
  readStartbelegCreatedAt,
} from '@/features/cash-registers/utils/registerStatus';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

export type CashRegisterDetailDrawerProps = {
  open: boolean;
  register: CashRegister | null;
  onClose: () => void;
  /** @deprecated Use CashRegisterStatusBadge. */
  statusLabel?: (status: number | undefined) => string;
  onHardDelete?: () => void;
  showHardDelete?: boolean;
};

export function CashRegisterDetailDrawer({
  open,
  register,
  onClose,
  onHardDelete,
  showHardDelete,
}: CashRegisterDetailDrawerProps) {
  const { t, formatLocale } = useI18n();
  const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
  const status = register ? rawRegisterStatus(register) : undefined;
  const decommissioned = isDecommissionedRegister(status);
  const decommissionMeta = register ? readDecommissionMeta(register) : null;
  const registerNumber = register?.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY;
  const enhanced = register as EnhancedCashRegister | null;
  const registerId = register?.id?.trim();
  const permissions = useCashRegisterPermissions(enhanced);

  const tseHealthQuery = useQuery({
    queryKey: ['admin', 'cash-registers', registerId, 'tse-health'],
    queryFn: () => getCashRegisterTseHealth(registerId!),
    enabled: open && Boolean(registerId),
    staleTime: 15_000,
  });

  const startbelegCreatedAt = readStartbelegCreatedAt(register);

  const offlineHref = registerId
    ? `/admin/tse/offline-transactions?cashRegisterId=${encodeURIComponent(registerId)}`
    : '/admin/tse/offline-transactions';

  return (
    <Drawer
      title={t('cashRegisters.detail.titleWithNumber', { number: registerNumber })}
      open={open}
      onClose={onClose}
      size={480}
      destroyOnHidden
    >
      {register ? (
        <CashRegisterStatusContextAlert register={register} showOpenPrerequisites />
      ) : null}
      <LimitWarning limitKey="maxActiveRegistersPerUser" />
      {register ? (
        <Descriptions column={1} bordered size="small">
          <Descriptions.Item label={t('cashRegisters.detail.location')}>
            {register.location?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.registerNumber')}>
            {register.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.columns.status')}>
            <CashRegisterStatusBadge register={register} />
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
            <CashierDisplay
              user={register.currentUser}
              displayName={enhanced?.currentCashierName}
              userName={enhanced?.currentCashierUserName ?? register.currentUser?.userName}
              email={enhanced?.currentCashierEmail ?? register.currentUser?.email}
            />
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.assignedUser')}>
            {registerId ? (
              <CashRegisterAssignedUserField
                registerId={registerId}
                assignedUserId={enhanced?.assignedUserId}
                assignedUserName={enhanced?.assignedUserName}
                canEdit={permissions.canAssignUser}
                disabled={decommissioned}
              />
            ) : (
              FORMAT_EMPTY_DISPLAY
            )}
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
            {startbelegCreatedAt
              ? formatDateTime(startbelegCreatedAt, formatLocale)
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
            <Space orientation="vertical" size={4}>
              <TseHealthBadge status={tseHealthQuery.data?.status ?? enhanced?.tseHealthStatus} />
              {tseHealthQuery.data?.message ? (
                <Typography.Text type="secondary">{tseHealthQuery.data.message}</Typography.Text>
              ) : null}
              <Space wrap>
                <Button icon={<SafetyOutlined />} size="small" href="/rksv/status">
                  {t('cashRegisters.detail.openTseDetails')}
                </Button>
                {(enhanced?.offlineQueueCount ?? 0) > 0 ? (
                  <Button icon={<CloudSyncOutlined />} size="small" href={offlineHref}>
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
              <Space orientation="vertical" size={0}>
                {enhanced.deviceInfo?.model ? (
                  <Typography.Text>
                    {t('cashRegisters.detail.deviceModel')}: {enhanced.deviceInfo.model}
                  </Typography.Text>
                ) : null}
                {enhanced.deviceInfo?.osVersion ? (
                  <Typography.Text>
                    {t('cashRegisters.detail.deviceOs')}: {enhanced.deviceInfo.osVersion}
                  </Typography.Text>
                ) : null}
                {enhanced.deviceInfo?.appVersion ? (
                  <Typography.Text>
                    {t('cashRegisters.detail.deviceApp')}: {enhanced.deviceInfo.appVersion}
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
      {register && canOpenSonderbelege ? (
        <>
          <Divider />
          <Typography.Title level={5}>
            {t('cashRegisters.detail.specialReceiptsTitle')}
          </Typography.Title>
          <Space wrap>
            {(
              [
                {
                  href: '/rksv/sonderbelege?focus=startbeleg',
                  label: t('receipts.specialKind.startbeleg'),
                  danger: false,
                },
                {
                  href: '/rksv/sonderbelege?focus=monatsbeleg',
                  label: t('receipts.specialKind.monatsbeleg'),
                  danger: false,
                },
                {
                  href: '/rksv/sonderbelege?focus=jahresbeleg',
                  label: t('receipts.specialKind.jahresbeleg'),
                  danger: false,
                },
                {
                  href: '/rksv/sonderbelege?focus=schlussbeleg',
                  label: t('receipts.specialKind.schlussbeleg'),
                  danger: true,
                },
              ] as const
            ).map((item) => {
              const button = (
                <Button
                  size="small"
                  danger={item.danger}
                  disabled={decommissioned}
                  href={decommissioned ? undefined : item.href}
                >
                  {item.label}
                </Button>
              );
              return decommissioned ? (
                <Tooltip
                  key={item.href}
                  title={t('cashRegisters.actions.decommissionedCannotCreateSpecialReceipts')}
                >
                  <span>{button}</span>
                </Tooltip>
              ) : (
                <span key={item.href}>{button}</span>
              );
            })}
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
