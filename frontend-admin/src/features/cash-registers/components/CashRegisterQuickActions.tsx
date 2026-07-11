'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import {
    DeleteOutlined,
    DownOutlined,
    FileTextOutlined,
    LockOutlined,
    UnlockOutlined,
} from '@ant-design/icons';
import { Dropdown, Tooltip } from 'antd';
import type { MenuProps } from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { CashRegister } from '@/api/generated/model';
import {
    postApiCashRegisterIdClose,
    postApiCashRegisterIdOpen,
} from '@/api/generated/cash-register/cash-register';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { invalidateShiftRelatedQueries } from '@/features/shifts/api/shiftQueryInvalidation';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';

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
  const { message } = useAntdApp();

    const { t } = useI18n();
    const queryClient = useQueryClient();
    const registerId = register.id?.trim();
    const status = rawRegisterStatus(register);
    const decommissioned = isDecommissionedRegister(status);
    const isOpen = status === REGISTER_STATUS.open;
    const isClosed = status === REGISTER_STATUS.closed;

    const openMutation = useMutation({
        mutationFn: () => postApiCashRegisterIdOpen(registerId!, { openingBalance: 0 }),
        onSuccess: async () => {
            message.success(t('cashRegisters.shift.openSuccess'));
            await invalidateShiftRelatedQueries(queryClient, registerId);
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'CashRegisterQuickActions.open',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const closeMutation = useMutation({
        mutationFn: () => postApiCashRegisterIdClose(registerId!, { closingBalance: register.currentBalance ?? 0 }),
        onSuccess: async () => {
            message.success(t('cashRegisters.shift.closeSuccess'));
            await invalidateShiftRelatedQueries(queryClient, registerId);
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'CashRegisterQuickActions.close',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    if (!registerId || decommissioned) {
        return null;
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
                label: t('cashRegisters.actions.closeRegister'),
                icon: <LockOutlined />,
                disabled: !isOpen || closeMutation.isPending,
                onClick: () => closeMutation.mutate(),
            },
            {
                key: 'receipts',
                label: t('cashRegisters.actions.viewReceipts'),
                icon: <FileTextOutlined />,
                onClick: () => {
                    globalThis.window.location.href = `/receipts?cashRegisterId=${encodeURIComponent(registerId)}`;
                },
            },
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
        return (
            <Tooltip title={t('cashRegisters.shiftGuidance.openActionTooltip')}>
                {dropdown}
            </Tooltip>
        );
    }

    return dropdown;
}
