'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Select, Space, Tooltip, Typography } from 'antd';
import type { SelectProps } from 'antd';
import { ShopOutlined } from '@ant-design/icons';

import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useI18n } from '@/i18n';

export type CashRegisterSelectorProps = {
    value?: string;
    onChange?: (registerId: string, registerNumber: string, tenantId?: string) => void;
    /** Super Admin: filter by mandant; when omitted, optional internal tenant picker or all tenants. */
    tenantId?: string;
    /** Super Admin: show mandant picker when `tenantId` is not passed. Default true. */
    showTenantPicker?: boolean;
    placeholder?: string;
    allowClear?: boolean;
    disabled?: boolean;
    style?: SelectProps['style'];
    className?: string;
};

export function CashRegisterSelector({
    value,
    onChange,
    tenantId: propTenantId,
    showTenantPicker = true,
    placeholder,
    allowClear = true,
    disabled = false,
    style,
    className,
}: CashRegisterSelectorProps) {
    const { t } = useI18n();
    const { tenantId: currentTenantId, isSuperAdminUser, tenantName, tenantSlug } = useCurrentTenant();
    const [pickedTenantId, setPickedTenantId] = useState<string | undefined>();

    const showMandantPicker = isSuperAdminUser && showTenantPicker && !propTenantId;
    const { tenants, isLoading: tenantsLoading } = useTenantList({
        enabled: showMandantPicker,
    });

    useEffect(() => {
        if (propTenantId) {
            setPickedTenantId(propTenantId);
        }
    }, [propTenantId]);

    const effectiveTenantId = isSuperAdminUser
        ? (propTenantId ?? pickedTenantId ?? currentTenantId ?? undefined)
        : (currentTenantId ?? undefined);

    const listAllTenants = isSuperAdminUser && !effectiveTenantId;

    const { registers, isLoading, error } = useAdminCashRegisterList({
        tenantId: effectiveTenantId,
        allowAllTenants: listAllTenants,
        enabled: isSuperAdminUser || Boolean(effectiveTenantId),
    });

    const tenantById = useMemo(
        () => new Map(tenants.map((row) => [row.id, row])),
        [tenants],
    );

    const activeTenantMeta = useMemo(() => {
        if (!effectiveTenantId) {
            return null;
        }
        const fromList = tenantById.get(effectiveTenantId);
        if (fromList) {
            return { name: fromList.name, slug: fromList.slug };
        }
        if (effectiveTenantId === currentTenantId) {
            return { name: tenantName ?? tenantSlug ?? effectiveTenantId, slug: tenantSlug ?? '' };
        }
        return { name: effectiveTenantId, slug: '' };
    }, [currentTenantId, effectiveTenantId, tenantById, tenantName, tenantSlug]);

    const mandantOptions = useMemo(
        () =>
            tenants.map((row) => ({
                value: row.id,
                label: t('cashRegisters.create.tenantOption', { name: row.name, slug: row.slug }),
            })),
        [tenants, t],
    );

    const registerOptions = useMemo(
        () =>
            registers.map((register) => ({
                value: register.id,
                label: register.registerNumber,
                register,
            })),
        [registers],
    );

    const handleRegisterChange = useCallback(
        (selectedId: string | undefined) => {
            if (!selectedId) {
                return;
            }
            const selected = registers.find((row) => row.id === selectedId);
            if (selected) {
                onChange?.(selected.id, selected.registerNumber, selected.tenantId);
            }
        },
        [onChange, registers],
    );

    const filterRegisterOption = useCallback((input: string, option?: { register?: AdminCashRegisterListItem }) => {
        const reg = option?.register;
        if (!reg) {
            return false;
        }
        const hay = `${reg.registerNumber} ${reg.location ?? ''}`.toLowerCase();
        return hay.includes(input.trim().toLowerCase());
    }, []);

    const renderRegisterOption = useCallback(
        (register: AdminCashRegisterListItem) => {
            const mandant =
                listAllTenants && register.tenantId
                    ? tenantById.get(register.tenantId)
                    : null;

            return (
                <div
                    style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        gap: 8,
                    }}
                >
                    <span>
                        <ShopOutlined style={{ marginRight: 8, color: '#1677ff' }} />
                        {register.registerNumber}
                        {mandant ? (
                            <Typography.Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                                {mandant.slug}
                            </Typography.Text>
                        ) : null}
                    </span>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {register.location?.trim() || t('cashRegisters.selector.noLocation')}
                    </Typography.Text>
                </div>
            );
        },
        [listAllTenants, t, tenantById],
    );

    const registerSelect = (() => {
        if (!isSuperAdminUser && !effectiveTenantId) {
            return (
                <Select
                    className={className}
                    style={style}
                    disabled
                    placeholder={t('cashRegisters.selector.noTenant')}
                    status="warning"
                />
            );
        }

        if (error) {
            return (
                <Tooltip title={t('cashRegisters.selector.loadErrorTooltip')}>
                    <Select
                        className={className}
                        style={style}
                        disabled
                        placeholder={t('cashRegisters.selector.loadError')}
                        status="error"
                    />
                </Tooltip>
            );
        }

        return (
            <Select
                className={className}
                style={style}
                value={value}
                onChange={handleRegisterChange}
                placeholder={placeholder ?? t('cashRegisters.selector.placeholder')}
                allowClear={allowClear}
                disabled={disabled || isLoading}
                loading={isLoading}
                options={registerOptions}
                showSearch
                filterOption={filterRegisterOption}
                optionRender={(option) => {
                    const reg = registers.find((row) => row.id === option.value);
                    return reg ? renderRegisterOption(reg) : option.label;
                }}
                popupRender={(menu) => (
                    <>
                        {isSuperAdminUser && activeTenantMeta ? (
                            <div
                                style={{
                                    padding: '8px 12px',
                                    borderBottom: '1px solid #f0f0f0',
                                    background: '#fafafa',
                                }}
                            >
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {t('cashRegisters.selector.mandantHeader', {
                                        name: activeTenantMeta.name,
                                        slug: activeTenantMeta.slug,
                                    })}
                                </Typography.Text>
                            </div>
                        ) : null}
                        {menu}
                        {registers.length === 0 && !isLoading ? (
                            <div style={{ padding: 12, textAlign: 'center' }}>
                                <Typography.Text type="secondary">
                                    {t('cashRegisters.selector.empty')}
                                </Typography.Text>
                            </div>
                        ) : null}
                    </>
                )}
            />
        );
    })();

    if (!showMandantPicker) {
        return registerSelect;
    }

    return (
        <Space orientation="vertical" size="small" style={{ width: '100%' }}>
            <Select
                placeholder={t('cashRegisters.create.tenantPlaceholder')}
                value={pickedTenantId ?? currentTenantId}
                onChange={(next) => setPickedTenantId(next)}
                options={mandantOptions}
                showSearch
                optionFilterProp="label"
                loading={tenantsLoading}
                allowClear
                style={{ width: '100%' }}
            />
            {registerSelect}
        </Space>
    );
}
