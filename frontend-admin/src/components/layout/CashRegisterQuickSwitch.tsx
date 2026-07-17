'use client';

import { useCallback, useMemo, useState } from 'react';
import { Button, Dropdown, Space, Tag, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { CheckCircleOutlined, ShopOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';

import { CashRegisterDetailsTooltip } from '@/components/CashRegisterDetailsTooltip';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import {
    FA_QUICK_CASH_REGISTER_QUERY_PARAM,
    readQuickCashRegisterId,
    writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

export type CashRegisterQuickSwitchProps = {
    isMobile?: boolean;
};

export function CashRegisterQuickSwitch({ isMobile = false }: CashRegisterQuickSwitchProps) {
    const { t } = useI18n();
    const router = useRouter();
    const { canViewCashRegisters } = usePermissions();
    const { tenantId, isSuperAdminUser, requiresTenantSelection } = useCurrentTenant();

    const listAllTenants = isSuperAdminUser && !tenantId && !requiresTenantSelection;
    const listEnabled = canViewCashRegisters && (isSuperAdminUser ? !requiresTenantSelection : true);

    const tenantSelection = useCashRegisterSelection({
        enabled: listEnabled && !listAllTenants,
        autoSelect: true,
        persistSelection: true,
    });
    const { setSelectedRegisterId: setTenantSelectedRegisterId } = tenantSelection;

    const allTenantsList = useAdminCashRegisterList({
        allowAllTenants: true,
        enabled: listEnabled && listAllTenants,
        pageSize: 50,
    });

    const registers = listAllTenants ? allTenantsList.registers : tenantSelection.registers;
    const isLoading = listAllTenants ? allTenantsList.isLoading : tenantSelection.isLoading;

    const [allTenantsRegisterId, setAllTenantsRegisterId] = useState<string | null>(() =>
        readQuickCashRegisterId(tenantId),
    );

    const selectedRegisterId = listAllTenants
        ? allTenantsRegisterId
        : (tenantSelection.selectedRegisterId ?? null);

    const activeRegister = useMemo(
        () => registers.find((row) => row.id === selectedRegisterId) ?? null,
        [registers, selectedRegisterId],
    );

    const navigateToRegister = useCallback(
        (register: AdminCashRegisterListItem) => {
            writeQuickCashRegisterId(register.id, register.tenantId ?? tenantId ?? null);
            if (listAllTenants) {
                setAllTenantsRegisterId(register.id);
            } else {
                setTenantSelectedRegisterId(register.id);
            }
            router.push(`/kassenverwaltung?${FA_QUICK_CASH_REGISTER_QUERY_PARAM}=${encodeURIComponent(register.id)}`);
        },
        [listAllTenants, router, setTenantSelectedRegisterId, tenantId],
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
                        minWidth: 220,
                    }}
                >
                    <span>
                        {formatRegisterDisplayLabel(register.registerNumber)}
                        {register.location?.trim() ? (
                            <Typography.Text type="secondary" style={{ marginLeft: 6, fontSize: 12 }}>
                                {register.location.trim()}
                            </Typography.Text>
                        ) : null}
                    </span>
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
    const registerNumber = activeRegister
        ? formatRegisterDisplayLabel(activeRegister.registerNumber)
        : t('adminShell.header.cashRegisterQuickSwitchLabel');
    const locationHint = activeRegister?.location?.trim() || null;
    const isOpen = activeRegister?.status === REGISTER_STATUS.open;
    const hasSelection = Boolean(activeRegister);

    return (
        <div
            className="cash-register-quick-switch"
            data-testid="admin-header-cash-register-context"
        >
            {!isMobile && hasSelection ? (
                <span className="cash-register-quick-switch-context-label">
                    {t('cashRegisters.selector.activeContextLabel')}
                </span>
            ) : null}
            <Dropdown
                menu={{
                    items: menuItems,
                    onClick: handleMenuClick,
                    selectedKeys: selectedRegisterId ? [selectedRegisterId] : [],
                }}
                trigger={['click']}
                placement="bottomLeft"
                classNames={{ root: 'admin-header-dropdown' }}
                getPopupContainer={getAdminHeaderPopupContainer}
            >
                <CashRegisterDetailsTooltip register={activeRegister} placement="bottomLeft">
                    <span
                        className={[
                            'cash-register-quick-switch-trigger-wrap',
                            hasSelection ? 'cash-register-quick-switch-trigger-wrap--active' : '',
                            isOpen ? 'cash-register-quick-switch-trigger-wrap--open' : '',
                            hasSelection && !isOpen ? 'cash-register-quick-switch-trigger-wrap--closed' : '',
                        ]
                            .filter(Boolean)
                            .join(' ')}
                    >
                        <Button
                            type="default"
                            size="small"
                            className="admin-header-tool-btn cash-register-quick-switch-trigger"
                            loading={isLoading}
                            aria-label={ariaLabel}
                            data-testid="admin-header-cash-register-quick-switch"
                        >
                            <Space size={6} className="cash-register-quick-switch-content">
                                <ShopOutlined className="cash-register-quick-switch-icon" aria-hidden />
                                <span className="cash-register-quick-switch-text">
                                    <span className="cash-register-quick-switch-label">{registerNumber}</span>
                                    {!isMobile && locationHint ? (
                                        <span className="cash-register-quick-switch-location">{locationHint}</span>
                                    ) : null}
                                </span>
                                {hasSelection && !isMobile ? (
                                    <Tag
                                        color="green"
                                        variant="filled"
                                        icon={<CheckCircleOutlined />}
                                        className="cash-register-quick-switch-active-tag"
                                    >
                                        {t('cashRegisters.selector.activeTag')}
                                    </Tag>
                                ) : null}
                                {hasSelection ? (
                                    <span
                                        className={[
                                            'cash-register-quick-switch-status',
                                            isOpen
                                                ? 'cash-register-quick-switch-status--open'
                                                : 'cash-register-quick-switch-status--closed',
                                        ].join(' ')}
                                        aria-label={
                                            isOpen
                                                ? t('cashRegisters.statusBadge.open.text')
                                                : t('cashRegisters.statusBadge.closed.generic.text')
                                        }
                                        role="img"
                                    />
                                ) : null}
                            </Space>
                        </Button>
                    </span>
                </CashRegisterDetailsTooltip>
            </Dropdown>
        </div>
    );
}
