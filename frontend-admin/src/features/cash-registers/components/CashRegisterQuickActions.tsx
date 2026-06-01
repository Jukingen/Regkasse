'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import {
    DeleteOutlined,
    DownOutlined,
    FileTextOutlined,
    LockOutlined,
    UnlockOutlined,
} from '@ant-design/icons';
import { Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { CashRegister } from '@/api/generated/model';
import {
    postApiCashRegisterIdClose,
    postApiCashRegisterIdOpen,
} from '@/api/generated/cash-register/cash-register';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { cashRegisterListQueryKey } from '@/features/cash-registers/api/cashRegisters';
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

    const invalidate = async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers'] }),
            queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey }),
            queryClient.invalidateQueries({ queryKey: ['cash-registers'] }),
        ]);
    };

    const openMutation = useMutation({
        mutationFn: () => postApiCashRegisterIdOpen(registerId!, { openingBalance: 0 }),
        onSuccess: async () => {
            message.success(t('cashRegisters.shift.openSuccess'));
            await invalidate();
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
            await invalidate();
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

    return (
        <Dropdown menu={{ items }} trigger={['click']}>
            <a onClick={(e) => e.preventDefault()}>
                {t('cashRegisters.actions.quickActions')} <DownOutlined />
            </a>
        </Dropdown>
    );
}
