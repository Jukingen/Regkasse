'use client';

import { useCallback, useMemo, useState } from 'react';
import { Button, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { useRouter } from 'next/navigation';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import {
    FA_QUICK_CASH_REGISTER_QUERY_PARAM,
    readQuickCashRegisterId,
    writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { useCashRegisters } from '@/features/cash-registers/hooks/useCashRegisters';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
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

    const listAllTenants = isSuperAdminUser && !tenantId && !requiresTenantSelection;
    const listEnabled =
        canViewCashRegisters && (isSuperAdminUser ? !requiresTenantSelection : Boolean(tenantId));

    const tenantScoped = useCashRegisters(tenantId ?? undefined, {
        enabled: listEnabled && !listAllTenants,
        syncQuickSwitch: true,
    });

    const allTenantsList = useAdminCashRegisterList({
        allowAllTenants: true,
        enabled: listEnabled && listAllTenants,
        pageSize: 50,
    });

    const registers = listAllTenants ? allTenantsList.registers : tenantScoped.registers;
    const isLoading = listAllTenants ? allTenantsList.isLoading : tenantScoped.isLoading;

    const [allTenantsRegisterId, setAllTenantsRegisterId] = useState<string | null>(() =>
        readQuickCashRegisterId(),
    );

    const selectedRegisterId = listAllTenants ? allTenantsRegisterId : tenantScoped.selectedRegisterId;

    const activeRegister = useMemo(
        () => registers.find((row) => row.id === selectedRegisterId) ?? null,
        [registers, selectedRegisterId],
    );

    const navigateToRegister = useCallback(
        (register: AdminCashRegisterListItem) => {
            writeQuickCashRegisterId(register.id);
            if (listAllTenants) {
                setAllTenantsRegisterId(register.id);
            }
            router.push(`/kassenverwaltung?${FA_QUICK_CASH_REGISTER_QUERY_PARAM}=${encodeURIComponent(register.id)}`);
        },
        [listAllTenants, router],
    );

    const menuItems: MenuProps['items'] = useMemo(() => {
        const rows: MenuProps['items'] = registers.map((register) => ({
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
                    <CashRegisterStatusBadge register={register as never} useIcon />
                </div>
            ),
        }));

        if (rows.length > 0) {
            rows.push({ type: 'divider' });
            rows.push({
                key: '__open-management__',
                label: t('adminShell.header.cashRegisterQuickSwitchManage'),
            });
        }

        return rows;
    }, [registers, t]);

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
    const buttonLabel = activeRegister?.registerNumber ?? t('adminShell.header.cashRegisterQuickSwitchLabel');

    return (
        <Dropdown
            menu={{
                items: menuItems,
                onClick: handleMenuClick,
                selectedKeys: selectedRegisterId ? [selectedRegisterId] : [],
            }}
            trigger={['click']}
            placement="bottomRight"
            classNames={{ root: 'admin-header-dropdown' }}
            getPopupContainer={getAdminHeaderPopupContainer}
        >
            <span className="cash-register-quick-switch-trigger-wrap">
                <Button
                    type="default"
                    size="small"
                    className="admin-header-tool-btn cash-register-quick-switch-trigger"
                    loading={isLoading}
                    aria-label={ariaLabel}
                    data-testid="admin-header-cash-register-quick-switch"
                >
                    <span className="cash-register-quick-switch-label">{buttonLabel}</span>
                </Button>
            </span>
        </Dropdown>
    );
}
