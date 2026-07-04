'use client';

import {
    CheckCircleOutlined,
    DeleteOutlined,
    DownOutlined,
    EditOutlined,
    LockOutlined,
    PlayCircleOutlined,
    StopOutlined,
    UnlockOutlined,
} from '@ant-design/icons';
import { Button, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/hooks/usePermissions';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';

export type CashRegisterActionKey =
    | 'open-shift'
    | 'close-shift'
    | 'daily-closing'
    | 'edit'
    | 'delete'
    | 'decommission';

export type CashRegisterActionsProps = {
    register: CashRegister;
    onAction: (key: CashRegisterActionKey, register: CashRegister) => void;
    /** Shift open/close and daily closing — Manager with cash_register.manage. */
    canOperate?: boolean;
};

export function CashRegisterActions({
    register,
    onAction,
    canOperate = true,
}: CashRegisterActionsProps) {
    const { t } = useI18n();
    const { isSuperAdmin } = usePermissions();

    const registerId = register.id?.trim();
    const status = rawRegisterStatus(register);
    const decommissioned = isDecommissionedRegister(status);
    const isOpen = status === REGISTER_STATUS.open;
    const isClosed = status === REGISTER_STATUS.closed;

    if (!registerId || decommissioned || !canOperate) {
        return null;
    }

    const items: MenuProps['items'] = [
        {
            key: 'open-shift',
            label: t('cashRegisters.actions.openShift'),
            icon: <UnlockOutlined />,
            disabled: !isClosed,
        },
        {
            key: 'close-shift',
            label: t('cashRegisters.actions.closeShift'),
            icon: <LockOutlined />,
            disabled: !isOpen,
        },
        {
            key: 'daily-closing',
            label: t('cashRegisters.actions.dailyClosing'),
            icon: <CheckCircleOutlined />,
        },
    ];

    if (isSuperAdmin) {
        items.push(
            { type: 'divider' },
            {
                key: 'edit',
                label: t('cashRegisters.actions.edit'),
                icon: <EditOutlined />,
            },
            {
                key: 'delete',
                label: t('cashRegisters.actions.delete'),
                icon: <DeleteOutlined />,
                danger: true,
            },
            {
                key: 'decommission',
                label: t('cashRegisters.actions.decommission'),
                icon: <StopOutlined />,
                danger: true,
                disabled: !canDecommissionRegister(status),
            },
        );
    }

    return (
        <Dropdown
            menu={{
                items,
                onClick: ({ key }) => onAction(key as CashRegisterActionKey, register),
            }}
            trigger={['click']}
        >
            <Button size="small" icon={<PlayCircleOutlined />} aria-label={t('cashRegisters.actions.quickActions')}>
                {t('cashRegisters.actions.quickActions')} <DownOutlined />
            </Button>
        </Dropdown>
    );
}
