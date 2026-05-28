'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Dropdown, Tag, Tooltip } from 'antd';
import type { MenuProps } from 'antd';
import { ShopOutlined, SwapOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import {
    FA_QUICK_CASH_REGISTER_QUERY_PARAM,
    readQuickCashRegisterId,
    writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import {
    REGISTER_STATUS,
    rawRegisterStatus,
    registerStatusTagColor,
} from '@/features/cash-registers/utils/registerStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

export type CashRegisterQuickSwitchProps = {
    isMobile?: boolean;
};

export function CashRegisterQuickSwitch({ isMobile = false }: CashRegisterQuickSwitchProps) {
    const { t } = useI18n();
    const router = useRouter();
    const { canViewCashRegisters } = usePermissions();
    const { tenantId, isSuperAdminUser, requiresTenantSelection } = useCurrentTenant();

    const [activeRegisterId, setActiveRegisterId] = useState<string | null>(null);

    useEffect(() => {
        setActiveRegisterId(readQuickCashRegisterId());
    }, []);

    const listAllTenants = isSuperAdminUser && !tenantId && !requiresTenantSelection;

    const { registers, isLoading } = useAdminCashRegisterList({
        tenantId: tenantId ?? undefined,
        allowAllTenants: listAllTenants,
        enabled: canViewCashRegisters && (isSuperAdminUser ? !requiresTenantSelection : Boolean(tenantId)),
        pageSize: 50,
    });

    const activeRegister = useMemo(
        () => registers.find((row) => row.id === activeRegisterId) ?? null,
        [activeRegisterId, registers],
    );

    const statusShortLabel = useCallback(
        (status: number | undefined) => {
            switch (status) {
                case REGISTER_STATUS.open:
                    return t('cashRegisters.status.open');
                case REGISTER_STATUS.closed:
                    return t('cashRegisters.status.closed');
                case REGISTER_STATUS.decommissioned:
                    return t('cashRegisters.status.decommissioned');
                default:
                    return t('cashRegisters.status.unknown', { status: String(status ?? '—') });
            }
        },
        [t],
    );

    const navigateToRegister = useCallback(
        (register: AdminCashRegisterListItem) => {
            writeQuickCashRegisterId(register.id);
            setActiveRegisterId(register.id);
            router.push(`/kassenverwaltung?${FA_QUICK_CASH_REGISTER_QUERY_PARAM}=${encodeURIComponent(register.id)}`);
        },
        [router],
    );

    const menuItems: MenuProps['items'] = useMemo(() => {
        const rows: MenuProps['items'] = registers.map((register) => {
            const status = rawRegisterStatus(register as never);
            return {
                key: register.id,
                label: (
                    <div
                        style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            gap: 8,
                            minWidth: 200,
                        }}
                    >
                        <span>{register.registerNumber}</span>
                        <Tag color={registerStatusTagColor(status)} style={{ fontSize: 10, margin: 0 }}>
                            {statusShortLabel(status)}
                        </Tag>
                    </div>
                ),
            };
        });

        if (rows.length > 0) {
            rows.push({ type: 'divider' });
            rows.push({
                key: '__open-management__',
                label: t('adminShell.header.cashRegisterQuickSwitchManage'),
            });
        }

        return rows;
    }, [registers, statusShortLabel, t]);

    const handleMenuClick: MenuProps['onClick'] = useCallback(
        ({ key }: { key: string }) => {
            if (key === '__open-management__') {
                router.push('/kassenverwaltung');
                return;
            }
            const register = registers.find((row) => row.id === key);
            if (register) {
                navigateToRegister(register);
            }
        },
        [navigateToRegister, registers, router],
    );

    if (!canViewCashRegisters) {
        return null;
    }

    if (requiresTenantSelection && isSuperAdminUser) {
        return null;
    }

    if (!isLoading && registers.length === 0) {
        return null;
    }

    const ariaLabel = t('adminShell.header.cashRegisterQuickSwitchAria');
    const tooltipHint = t('adminShell.header.cashRegisterQuickSwitchHint');
    const buttonLabel = activeRegister?.registerNumber ?? t('adminShell.header.cashRegisterQuickSwitchLabel');

    return (
        <Dropdown
            menu={{
                items: menuItems,
                onClick: handleMenuClick,
                selectedKeys: activeRegisterId ? [activeRegisterId] : [],
            }}
            trigger={['click']}
            placement="bottomRight"
            overlayClassName="admin-header-dropdown"
            getPopupContainer={getAdminHeaderPopupContainer}
        >
            <Tooltip title={tooltipHint} placement="bottomRight" mouseEnterDelay={0.35}>
                <span className="cash-register-quick-switch-trigger-wrap">
                    <Button
                        type="text"
                        className="cash-register-quick-switch-trigger"
                        icon={<ShopOutlined />}
                        loading={isLoading}
                        aria-label={ariaLabel}
                        data-testid="admin-header-cash-register-quick-switch"
                    >
                        {!isMobile ? (
                            <>
                                <span className="cash-register-quick-switch-label">{buttonLabel}</span>
                                <SwapOutlined aria-hidden />
                            </>
                        ) : null}
                    </Button>
                </span>
            </Tooltip>
        </Dropdown>
    );
}
