'use client';

import {
  DeleteOutlined,
  DownOutlined,
  FileTextOutlined,
  LockOutlined,
  UnlockOutlined,
} from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { MenuProps } from 'antd';
import { Button, Dropdown, Tooltip } from 'antd';

import type { CashRegister } from '@/api/generated/model';
import { closeCashRegister, openCashRegister } from '@/features/cash-registers/api/cashRegisters';
import { confirmForceCloseHeldByOther } from '@/features/cash-registers/utils/confirmForceCloseHeldByOther';
import {
  REGISTER_STATUS,
  canDecommissionRegister,
  isDecommissionedRegister,
  rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import {
  isOpenShiftHeldBy,
  resolveOpenShiftHolderName,
} from '@/features/cash-registers/utils/shiftOccupancy';
import { forceCloseAdminShiftRegister } from '@/features/shifts/api/shiftsOverview';
import { invalidateShiftRelatedQueries } from '@/features/shifts/api/shiftQueryInvalidation';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

export type CashRegisterQuickActionsProps = {
  register: CashRegister;
  canManage?: boolean;
  canDecommission?: boolean;
  onViewDetail?: () => void;
  onDecommission?: () => void;
};

export function CashRegisterQuickActions({
  register,
  canManage = false,
  canDecommission = false,
  onDecommission,
}: CashRegisterQuickActionsProps) {
  const { message, modal } = useAntdApp();

  const { t } = useI18n();
  const { user, hasPermission, isSuperAdmin } = usePermissions();
  const queryClient = useQueryClient();
  const registerId = register.id?.trim();
  const status = rawRegisterStatus(register);
  const decommissioned = isDecommissionedRegister(status);
  const isOpen = status === REGISTER_STATUS.open;
  const isClosed = status === REGISTER_STATUS.closed;
  const holdsOpenShift = isOpenShiftHeldBy(register.currentUserId, user?.id);
  const canForceClose = isSuperAdmin || hasPermission(PERMISSIONS.SHIFT_MANAGE);
  const canCloseThisShift = isOpen && (holdsOpenShift || canForceClose);

  const openMutation = useMutation({
    mutationFn: () => openCashRegister(registerId!, { openingBalance: 0 }),
    onSuccess: async () => {
      message.success(t('cashRegisters.shift.openSuccess'));
      await invalidateShiftRelatedQueries(queryClient, registerId);
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'CashRegisterQuickActions.open',
          fallbackKey: 'common.messages.unknownError',
        })
      );
    },
  });

  const closeMutation = useMutation({
    mutationFn: () =>
      closeCashRegister(registerId!, { closingBalance: register.currentBalance ?? 0 }),
    onSuccess: async () => {
      message.success(t('cashRegisters.shift.closeSuccess'));
      await invalidateShiftRelatedQueries(queryClient, registerId);
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'CashRegisterQuickActions.close',
          fallbackKey: 'common.messages.unknownError',
        })
      );
    },
  });

  const forceCloseMutation = useMutation({
    mutationFn: () =>
      forceCloseAdminShiftRegister(registerId!, {
        closingBalance: register.currentBalance ?? 0,
        reason: 'Kassenverwaltung recovery close',
      }),
    onSuccess: async () => {
      message.success(t('cashRegisters.shift.closeSuccess'));
      await invalidateShiftRelatedQueries(queryClient, registerId);
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'CashRegisterQuickActions.forceClose',
          fallbackKey: 'shifts.actions.forceCloseFailed',
        })
      );
    },
  });

  const handleClose = () => {
    if (holdsOpenShift) {
      closeMutation.mutate();
      return;
    }
    if (canForceClose) {
      const holder =
        resolveOpenShiftHolderName(register) || t('cashRegisters.shift.unknownHolder');
      confirmForceCloseHeldByOther(modal, t, holder, () => forceCloseMutation.mutate());
    }
  };

  if (!registerId) {
    return null;
  }

  if (decommissioned) {
    return (
      <Tooltip title={t('cashRegisters.actions.decommissionedCannotOpen')}>
        <span>
          <Button
            size="small"
            icon={<UnlockOutlined />}
            disabled
            aria-label={t('cashRegisters.actions.openRegister')}
          >
            {t('cashRegisters.actions.openRegister')}
          </Button>
        </span>
      </Tooltip>
    );
  }

  const items: MenuProps['items'] = [];

  if (canManage) {
    items.push(
      {
        key: 'open',
        label: t('cashRegisters.actions.openRegister'),
        icon: <UnlockOutlined />,
        disabled: !isClosed || openMutation.isPending,
        onClick: () => openMutation.mutate(),
      },
      {
        key: 'close',
        label:
          isOpen && !holdsOpenShift && !canForceClose
            ? (
                <Tooltip title={t('cashRegisters.shift.closeHeldByOther')}>
                  <span>{t('cashRegisters.actions.closeRegister')}</span>
                </Tooltip>
              )
            : t('cashRegisters.actions.closeRegister'),
        icon: <LockOutlined />,
        disabled: !canCloseThisShift || closeMutation.isPending || forceCloseMutation.isPending,
        onClick: handleClose,
      },
      {
        key: 'receipts',
        label: t('cashRegisters.actions.viewReceipts'),
        icon: <FileTextOutlined />,
        onClick: () => {
          globalThis.window.location.href = `/receipts?cashRegisterId=${encodeURIComponent(registerId)}`;
        },
      }
    );
  }

  if (canDecommission && canDecommissionRegister(status)) {
    items.push({ type: 'divider' });
    items.push({
      key: 'decommission',
      label: t('cashRegisters.actions.decommission'),
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => onDecommission?.(),
    });
  }

  if (!items.length) {
    return null;
  }

  const dropdown = (
    <Dropdown menu={{ items }} trigger={['click']}>
      <a onClick={(e) => e.preventDefault()}>
        {t('cashRegisters.actions.quickActions')} <DownOutlined />
      </a>
    </Dropdown>
  );

  if (canManage && isClosed) {
    return <Tooltip title={t('cashRegisters.shiftGuidance.openActionTooltip')}>{dropdown}</Tooltip>;
  }

  return dropdown;
}
