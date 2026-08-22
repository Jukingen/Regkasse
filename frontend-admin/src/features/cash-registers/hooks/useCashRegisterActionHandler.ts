'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useCallback } from 'react';

import type { CashRegister } from '@/api/generated/model';
import { closeCashRegister, openCashRegister } from '@/features/cash-registers/api/cashRegisters';
import type { CashRegisterActionKey } from '@/features/cash-registers/components/CashRegisterActions';
import { FA_QUICK_CASH_REGISTER_QUERY_PARAM } from '@/features/cash-registers/constants/quickSwitch';
import { confirmForceCloseHeldByOther } from '@/features/cash-registers/utils/confirmForceCloseHeldByOther';
import {
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

type UseCashRegisterActionHandlerOptions = {
  onEdit: (register: CashRegister) => void;
  onDecommission: (register: CashRegister) => void;
  onHardDelete: (register: CashRegister) => void;
};

export function useCashRegisterActionHandler({
  onEdit,
  onDecommission,
  onHardDelete,
}: UseCashRegisterActionHandlerOptions) {
  const { message, modal } = useAntdApp();
  const { t } = useI18n();
  const { user, hasPermission, isSuperAdmin } = usePermissions();
  const router = useRouter();
  const queryClient = useQueryClient();
  const canForceClose = isSuperAdmin || hasPermission(PERMISSIONS.SHIFT_MANAGE);

  const openMutation = useMutation({
    mutationFn: (register: CashRegister) =>
      openCashRegister(register.id!.trim(), { openingBalance: 0 }),
    onSuccess: async (_data, register) => {
      message.success(t('cashRegisters.shift.openSuccess'));
      await invalidateShiftRelatedQueries(queryClient, register.id?.trim());
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'CashRegisterActions.openShift',
          fallbackKey: 'common.messages.unknownError',
        })
      );
    },
  });

  const closeMutation = useMutation({
    mutationFn: (register: CashRegister) =>
      closeCashRegister(register.id!.trim(), {
        closingBalance: register.currentBalance ?? 0,
      }),
    onSuccess: async (_data, register) => {
      message.success(t('cashRegisters.shift.closeSuccess'));
      await invalidateShiftRelatedQueries(queryClient, register.id?.trim());
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'CashRegisterActions.closeShift',
          fallbackKey: 'common.messages.unknownError',
        })
      );
    },
  });

  const forceCloseMutation = useMutation({
    mutationFn: (register: CashRegister) =>
      forceCloseAdminShiftRegister(register.id!.trim(), {
        closingBalance: register.currentBalance ?? 0,
        reason: 'Kassenverwaltung recovery close',
      }),
    onSuccess: async (_data, register) => {
      message.success(t('cashRegisters.shift.closeSuccess'));
      await invalidateShiftRelatedQueries(queryClient, register.id?.trim());
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'CashRegisterActions.forceCloseShift',
          fallbackKey: 'shifts.actions.forceCloseFailed',
        })
      );
    },
  });

  const handleRegisterAction = useCallback(
    (key: CashRegisterActionKey, register: CashRegister) => {
      const registerId = register.id?.trim();
      if (!registerId) {
        return;
      }

      const decommissioned = isDecommissionedRegister(rawRegisterStatus(register));

      switch (key) {
        case 'open-shift':
          if (decommissioned || openMutation.isPending) return;
          openMutation.mutate(register);
          break;
        case 'close-shift':
          if (decommissioned || closeMutation.isPending || forceCloseMutation.isPending) return;
          if (isOpenShiftHeldBy(register.currentUserId, user?.id)) {
            closeMutation.mutate(register);
            return;
          }
          if (canForceClose) {
            const holder =
              resolveOpenShiftHolderName(register) || t('cashRegisters.shift.unknownHolder');
            confirmForceCloseHeldByOther(modal, t, holder, () =>
              forceCloseMutation.mutate(register)
            );
            return;
          }
          message.warning(t('cashRegisters.shift.closeHeldByOther'));
          break;
        case 'daily-closing':
          if (decommissioned) return;
          router.push(
            `/tagesabschluss?${FA_QUICK_CASH_REGISTER_QUERY_PARAM}=${encodeURIComponent(registerId)}`
          );
          break;
        case 'edit':
          onEdit(register);
          break;
        case 'delete':
          onHardDelete(register);
          break;
        case 'decommission':
          onDecommission(register);
          break;
        default:
          break;
      }
    },
    [
      canForceClose,
      closeMutation,
      forceCloseMutation,
      message,
      modal,
      onDecommission,
      onEdit,
      onHardDelete,
      openMutation,
      router,
      t,
      user?.id,
    ]
  );

  return {
    handleRegisterAction,
    canForceClose,
    shiftActionPending:
      openMutation.isPending || closeMutation.isPending || forceCloseMutation.isPending,
  };
}
